# Security and Compliance Strategy

This document outlines the security measures, data protection compliance approaches, and audit logging implemented in the Golf Tournament Organizer application, with particular attention to payment processing security requirements.

## Security Architecture Overview

Our security architecture implements defense-in-depth with multiple layers of protection:

1. **Authentication & Authorization**: Secure user identity verification and permission enforcement
2. **Data Protection**: Encryption of sensitive data at rest and in transit
3. **Infrastructure Security**: Secure hosting, network controls, and monitoring
4. **Application Security**: Secure coding practices, dependency management, and vulnerability scanning
5. **Operational Security**: Secure DevOps practices, access control, and incident response
6. **Compliance Framework**: Adherence to relevant regulations and industry standards

## Authentication and Authorization

### Authentication Implementation

The application uses Supabase Auth with the following security controls:

```typescript
// src/lib/supabase.ts
export const initializeAuth = () => {
  const authConfig = {
    // Minimum password requirements
    passwordMinLength: 10,
    passwordRequireUppercase: true,
    passwordRequireNumber: true,
    passwordRequireSpecial: true,
    
    // Session security
    sessionMaxInactivityMinutes: 30,
    refreshTokenRotationEnabled: true,
    
    // MFA options
    mfaEnabled: true,
    mfaPreferredMethod: 'totp',
    mfaRequiredForAdminUsers: true,
    
    // Rate limiting
    loginAttemptsBeforeCaptcha: 3,
    loginAttemptsBeforeLockout: 5,
    lockoutDurationMinutes: 15,
    
    // JWT configuration
    jwtExpirySeconds: 3600, // 1 hour
    refreshTokenExpiryDays: 7,
  };
  
  // Configure auth with Supabase
  return authConfig;
};
```

### Role-Based Access Control (RBAC)

Fine-grained permissions through Supabase RLS policies:

```sql
-- Example RLS policy for sensitive payment data
CREATE POLICY "Restrict payment data access" ON payment_transactions
  FOR SELECT
  USING (
    -- Only transaction owner or admin can view
    auth.uid() = user_id OR
    EXISTS (
      SELECT 1 FROM users
      WHERE id = auth.uid() AND role = 'admin'
    )
  );

-- Read-only access for tournament organizers
CREATE POLICY "Tournament organizers view only" ON payment_transactions
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM tournament_organizers
      WHERE user_id = auth.uid() 
      AND tournament_id = (
        SELECT tournament_id FROM tournament_registrations
        WHERE payment_id = payment_transactions.id
      )
    )
  );
```

### API Authentication

Secure API access using JWT validation:

```csharp
// GolfApp.Api/Auth/JwtAuthenticationHandler.cs
public class JwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenValidator _tokenValidator;
    private readonly ILogger<JwtAuthenticationHandler> _logger;

    public JwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ITokenValidator tokenValidator)
        : base(options, logger, encoder, clock)
    {
        _tokenValidator = tokenValidator;
        _logger = logger.CreateLogger<JwtAuthenticationHandler>();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract token from Authorization header
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.NoResult();
        }

        string authorizationHeader = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            return AuthenticateResult.NoResult();
        }

        string token = authorizationHeader.Substring("Bearer ".Length).Trim();

        try
        {
            // Validate JWT issued by Supabase
            var validationResult = await _tokenValidator.ValidateTokenAsync(token);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Invalid token: {Reason}", validationResult.FailureReason);
                return AuthenticateResult.Fail("Invalid token");
            }

            // Create claims principal from token claims
            var claimsPrincipal = validationResult.ClaimsPrincipal;
            var ticket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);
            
            _logger.LogInformation("Successfully authenticated user {UserId}", 
                claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier));
                
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token validation");
            return AuthenticateResult.Fail("Token validation failed");
        }
    }
}
```

## Data Protection and Encryption

### Data Classification

We classify data into tiers to determine appropriate security controls:

| Classification | Examples | Protection Level | Access Control |
|---------------|----------|-----------------|----------------|
| **Public** | Tournament schedules, public results | Basic | No restriction |
| **Internal** | User profiles, tournament details | Standard | User authentication |
| **Sensitive** | Personal contact details, payment methods | Enhanced | Strong authentication, need-to-know |
| **Restricted** | Payment transaction data, credit cards | Maximum | Strict isolation, encryption |

### Data Encryption Implementation

```typescript
// src/lib/encryption.ts
import { createCipheriv, createDecipheriv, randomBytes } from 'crypto';

// For client-side encryption of sensitive data before transmission
export const encryptSensitiveData = (data: string, publicKey: string): EncryptedData => {
  // Generate random initialization vector
  const iv = randomBytes(16);
  
  // Encrypt data with AES-256-GCM using public key
  const cipher = createCipheriv('aes-256-gcm', Buffer.from(publicKey, 'base64'), iv);
  
  // Encrypt the data
  let encryptedData = cipher.update(data, 'utf8', 'base64');
  encryptedData += cipher.final('base64');
  
  // Get authentication tag
  const authTag = cipher.getAuthTag().toString('base64');
  
  return {
    encryptedData,
    iv: iv.toString('base64'),
    authTag
  };
};
```

For sensitive data at rest in Supabase, we use column-level encryption:

```sql
-- Define encryption functions
CREATE OR REPLACE FUNCTION encrypt_pii(input_text TEXT, context TEXT)
RETURNS TEXT AS $$
  SELECT encode(
    pgp_sym_encrypt(
      input_text, 
      current_setting('app.encryption_key', true),
      'cipher-algo=aes256, compress-algo=2, compress-level=9'
    ),
    'base64'
  );
$$ LANGUAGE sql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION decrypt_pii(encrypted_text TEXT, context TEXT)
RETURNS TEXT AS $$
  SELECT pgp_sym_decrypt(
    decode(encrypted_text, 'base64'),
    current_setting('app.encryption_key', true)
  )::text;
$$ LANGUAGE sql SECURITY DEFINER;

-- Apply to sensitive columns
ALTER TABLE user_profiles 
  ALTER COLUMN phone_number 
  TYPE TEXT GENERATED ALWAYS AS (encrypt_pii(phone_number_raw, 'user_phone')) STORED,
  ADD COLUMN phone_number_raw TEXT;
  
-- Create view for authorized access
CREATE VIEW authorized_user_profiles AS
SELECT 
  id,
  user_id,
  decrypt_pii(phone_number, 'user_phone') as phone_number,
  -- Other columns
FROM user_profiles
WHERE (SELECT auth.uid() = user_id OR auth.role() = 'admin');
```

### Payment Data Security

For payment processing, we follow PCI-DSS requirements:

```typescript
// src/features/payments/hooks/usePaymentProcessing.ts
export const usePaymentProcessing = () => {
  const stripePromise = loadStripe(process.env.REACT_APP_STRIPE_PUBLIC_KEY);
  
  const processPayment = async (paymentDetails: PaymentDetails): Promise<PaymentResult> => {
    try {
      // Use Stripe Elements to avoid handling card data directly
      const stripe = await stripePromise;
      const elements = stripe.elements();
      
      // Create payment intent via our API (serverless function)
      const { data: paymentIntent } = await api.post('/api/create-payment-intent', {
        amount: paymentDetails.amount,
        currency: paymentDetails.currency,
        description: paymentDetails.description,
        metadata: {
          tournamentId: paymentDetails.tournamentId,
          userId: paymentDetails.userId,
        }
      });
      
      // Confirm payment with Stripe.js (client-side)
      const result = await stripe.confirmCardPayment(paymentIntent.client_secret, {
        payment_method: {
          card: elements.getElement('card'),
          billing_details: {
            name: paymentDetails.billingName,
            email: paymentDetails.billingEmail,
          }
        }
      });
      
      if (result.error) {
        throw new Error(result.error.message);
      }
      
      // Save transaction reference (not card details) to our database
      await api.post('/api/record-payment', {
        paymentIntentId: result.paymentIntent.id,
        tournamentId: paymentDetails.tournamentId,
        status: result.paymentIntent.status,
      });
      
      return {
        success: true,
        transactionId: result.paymentIntent.id,
        status: result.paymentIntent.status,
      };
      
    } catch (error) {
      console.error('Payment processing error:', error);
      return {
        success: false,
        error: error.message,
      };
    }
  };
  
  return { processPayment };
};
```

## Infrastructure Security

### Network Security

Implementation of network security controls in Azure:

```typescript
// infra/network-security.ts
const networkSecurityConfig = {
  webAppFirewall: {
    enabled: true,
    rulesets: ['OWASP3.2', 'BotProtection'],
    customRules: [
      {
        name: 'RateLimitByIP',
        priority: 100,
        action: 'Block',
        conditions: [
          {
            matchVariable: 'RemoteAddr',
            operator: 'RateLimit',
            matchValues: ['10'],
            selector: 'RequestsPerMinute'
          }
        ]
      },
      {
        name: 'BlockSuspiciousUserAgents',
        priority: 200,
        action: 'Block',
        conditions: [
          {
            matchVariable: 'RequestHeader',
            selector: 'User-Agent',
            operator: 'Contains',
            matchValues: ['sqlmap', 'nikto', 'nessus']
          }
        ]
      }
    ]
  },
  ddosProtection: {
    enabled: true,
    plan: 'Standard'
  },
  privateEndpoints: {
    enabled: true,
    services: ['sql', 'storage', 'functions']
  },
  networkIsolation: {
    virtualNetwork: {
      name: 'golf-app-vnet',
      addressSpace: '10.0.0.0/16',
      subnets: [
        {
          name: 'frontend-subnet',
          addressPrefix: '10.0.1.0/24',
          serviceEndpoints: ['Microsoft.Storage', 'Microsoft.Web']
        },
        {
          name: 'function-subnet',
          addressPrefix: '10.0.2.0/24',
          serviceEndpoints: ['Microsoft.Storage', 'Microsoft.Web', 'Microsoft.Sql']
        }
      ]
    }
  }
};
```

### Secure Configuration

Security hardening for Azure Functions:

```csharp
// GolfApp.Api/Startup.cs
public void ConfigureHttpPipeline(IFunctionsHostBuilder builder)
{
    builder.Services.AddSingleton<IHttpResponseInterceptor, SecurityHeadersInterceptor>();
    builder.Services.Configure<SecurityHeadersOptions>(options =>
    {
        options.Headers = new Dictionary<string, string>
        {
            { "X-Content-Type-Options", "nosniff" },
            { "X-Frame-Options", "DENY" },
            { "X-XSS-Protection", "1; mode=block" },
            { "Referrer-Policy", "strict-origin-when-cross-origin" },
            { "Content-Security-Policy", "default-src 'self'; script-src 'self'; object-src 'none'" },
            { "Permissions-Policy", "camera=(), microphone=(), geolocation=(self), payment=(self)" }
        };
    });
}

public class SecurityHeadersInterceptor : IHttpResponseInterceptor
{
    private readonly IDictionary<string, string> _headers;

    public SecurityHeadersInterceptor(IOptions<SecurityHeadersOptions> options)
    {
        _headers = options.Value.Headers;
    }

    public Task InterceptResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        foreach (var header in _headers)
        {
            response.Headers.Add(header.Key, header.Value);
        }
        
        return Task.CompletedTask;
    }
}
```

## Application Security

### Secure Coding Practices

Implementation of secure coding guidelines:

```typescript
// src/lib/input-validation.ts
import { z } from 'zod';

// Strong validation schemas for all user inputs
export const userProfileSchema = z.object({
  displayName: z
    .string()
    .min(2, 'Display name must be at least 2 characters')
    .max(50, 'Display name cannot exceed 50 characters')
    .regex(/^[a-zA-Z0-9\s\-_.]+$/, 'Display name contains invalid characters'),
  
  email: z
    .string()
    .email('Invalid email format')
    .toLowerCase(),
  
  phoneNumber: z
    .string()
    .regex(/^\+?[0-9\s\-()]+$/, 'Invalid phone number format')
    .optional(),
  
  birthYear: z
    .number()
    .int()
    .min(1900, 'Birth year must be after 1900')
    .max(new Date().getFullYear() - 10, 'Birth year is not valid')
    .optional(),
});

// Validate incoming data before processing
export const validateUserInput = <T>(schema: z.ZodSchema<T>, data: unknown): ValidationResult<T> => {
  try {
    const validated = schema.parse(data);
    return { success: true, data: validated };
  } catch (error) {
    if (error instanceof z.ZodError) {
      return {
        success: false,
        errors: error.errors.reduce((acc, err) => {
          const path = err.path.join('.');
          acc[path] = err.message;
          return acc;
        }, {} as Record<string, string>)
      };
    }
    return { success: false, errors: { _general: 'Invalid input data' } };
  }
};
```

### Dependency Security

CI/CD pipeline integration for dependency scanning:

```yaml
# .github/workflows/security-scan.yml
name: Security Scan

on:
  push:
    branches: [main, staging, develop]
  pull_request:
    branches: [main, staging]
  schedule:
    - cron: '0 2 * * 1'  # Weekly at 2am on Mondays

jobs:
  dependency-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'
          
      - name: Install dependencies
        run: npm ci
        
      - name: Run npm audit
        run: npm audit --audit-level=high
        
      - name: Run Snyk scan
        uses: snyk/actions/node@master
        with:
          args: --severity-threshold=high
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
          
  code-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Run ESLint security rules
        run: npx eslint . --config .eslintrc.security.js
        
      - name: Run SonarCloud scan
        uses: SonarSource/sonarcloud-github-action@master
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

## Audit Logging and Monitoring

### Comprehensive Audit Logging

Implementation of detailed audit logs for security events:

```typescript
// src/lib/audit-logging.ts
import { supabase } from './supabase';

export enum AuditEventType {
  // Authentication events
  LOGIN_SUCCESS = 'auth.login.success',
  LOGIN_FAILURE = 'auth.login.failure',
  PASSWORD_CHANGE = 'auth.password.change',
  MFA_ENABLED = 'auth.mfa.enabled',
  MFA_DISABLED = 'auth.mfa.disabled',
  
  // Authorization events
  PERMISSION_GRANTED = 'auth.permission.granted',
  PERMISSION_REVOKED = 'auth.permission.revoked',
  ADMIN_ACTION = 'auth.admin.action',
  
  // Data access events
  SENSITIVE_DATA_VIEW = 'data.sensitive.view',
  SENSITIVE_DATA_EXPORT = 'data.sensitive.export',
  BULK_DATA_ACCESS = 'data.bulk.access',
  
  // Financial events
  PAYMENT_CREATED = 'payment.created',
  PAYMENT_PROCESSED = 'payment.processed',
  REFUND_INITIATED = 'payment.refund.initiated',
  REFUND_PROCESSED = 'payment.refund.processed',
  
  // System events
  CONFIG_CHANGE = 'system.config.change',
  API_KEY_CREATED = 'system.apikey.created',
  API_KEY_REVOKED = 'system.apikey.revoked',
}

export interface AuditLogEntry {
  eventType: AuditEventType;
  userId: string | null;
  resourceType: string;
  resourceId: string | null;
  action: string;
  status: 'success' | 'failure';
  metadata: Record<string, any>;
  ipAddress: string;
  userAgent: string;
  timestamp: Date;
}

export const logAuditEvent = async (
  eventType: AuditEventType,
  details: Omit<AuditLogEntry, 'eventType' | 'timestamp' | 'ipAddress' | 'userAgent'>
): Promise<void> => {
  try {
    // Get client IP and user agent from browser
    const ipAddress = await fetch('https://api.ipify.org').then(res => res.text());
    const userAgent = navigator.userAgent;
    
    // Create audit log entry
    const logEntry: AuditLogEntry = {
      ...details,
      eventType,
      timestamp: new Date(),
      ipAddress,
      userAgent
    };
    
    // Store in Supabase
    await supabase.from('audit_logs').insert([logEntry]);
    
    // For high-severity events, also send to real-time monitoring
    if (
      eventType.startsWith('auth.') || 
      eventType.startsWith('payment.') || 
      eventType === AuditEventType.SENSITIVE_DATA_EXPORT
    ) {
      await fetch('/api/security-monitor', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(logEntry)
      });
    }
  } catch (error) {
    console.error('Failed to log audit event:', error);
    
    // Fallback to local storage if connection fails
    try {
      const pendingLogs = JSON.parse(localStorage.getItem('pendingAuditLogs') || '[]');
      pendingLogs.push({
        ...details,
        eventType,
        timestamp: new Date().toISOString(),
        ipAddress: 'unknown',
        userAgent: navigator.userAgent
      });
      localStorage.setItem('pendingAuditLogs', JSON.stringify(pendingLogs));
    } catch (e) {
      // Last resort console log
      console.error('Critical audit log failure:', e);
    }
  }
};
```

### Sensitive Action Logging

Server-side implementation for payment events:

```csharp
// GolfApp.Api/Services/AuditService.cs
public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;
    
    public AuditService(
        IRepository<AuditLog> repository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }
    
    public async Task LogPaymentEventAsync(PaymentEventType eventType, PaymentTransaction transaction)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
        
        // Create sanitized version of transaction for audit log
        // Never log full payment details/PAN
        var sanitizedData = new
        {
            transaction.Id,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            LastFour = transaction.CardDetails?.LastFour,
            transaction.CreatedAt,
            transaction.TournamentId,
            transaction.UserId
        };
        
        var auditLog = new AuditLog
        {
            EventType = eventType.ToString(),
            UserId = userId,
            ResourceType = "PaymentTransaction",
            ResourceId = transaction.Id,
            Action = GetActionFromEventType(eventType),
            Status = transaction.Status == "succeeded" ? "success" : "failure",
            Metadata = JsonSerializer.Serialize(sanitizedData),
            IpAddress = ipAddress ?? "unknown",
            UserAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "unknown",
            Timestamp = DateTime.UtcNow
        };
        
        // Store in database
        await _repository.CreateAsync(auditLog);
        
        // High-severity payment events also go to Application Insights
        _logger.LogInformation(
            "Payment event {EventType} for transaction {TransactionId} by user {UserId} with status {Status}",
            eventType, transaction.Id, userId, transaction.Status);
    }
    
    private string GetActionFromEventType(PaymentEventType eventType)
    {
        return eventType switch
        {
            PaymentEventType.Created => "create",
            PaymentEventType.Processed => "process",
            PaymentEventType.Failed => "fail",
            PaymentEventType.Refunded => "refund",
            PaymentEventType.Disputed => "dispute",
            _ => "unknown"
        };
    }
}
```

## Compliance Framework

### GDPR Compliance

Implementation of GDPR-required functionality:

```typescript
// src/features/profile/hooks/useGdprDataAccess.ts
export const useGdprDataAccess = (userId: string) => {
  const { isAdmin } = useAuth();
  
  // Data export functionality
  const exportUserData = async (): Promise<DataExportResult> => {
    // Log this sensitive action
    await logAuditEvent(AuditEventType.SENSITIVE_DATA_EXPORT, {
      userId,
      resourceType: 'user',
      resourceId: userId,
      action: 'export',
      status: 'success',
      metadata: { reason: 'GDPR data request' }
    });
    
    // Call data export API with appropriate authentication
    const { data, error } = await supabase.functions.invoke('gdpr-data-export', {
      body: { userId }
    });
    
    if (error) {
      return { success: false, error: error.message };
    }
    
    return { success: true, data };
  };
  
  // Right to be forgotten implementation
  const deleteUserData = async (): Promise<{ success: boolean; message?: string }> => {
    // This requires additional verification for security
    const verificationToken = await requestDeletionVerification(userId);
    
    // Confirm deletion with token
    const { data, error } = await supabase.functions.invoke('gdpr-delete-account', {
      body: { 
        userId,
        verificationToken
      }
    });
    
    if (error) {
      return { success: false, message: error.message };
    }
    
    await logAuditEvent(AuditEventType.ADMIN_ACTION, {
      userId,
      resourceType: 'user',
      resourceId: userId,
      action: 'delete',
      status: 'success',
      metadata: { reason: 'GDPR deletion request' }
    });
    
    return { success: true, message: 'Account deletion initiated' };
  };
  
  // Right to rectification implementation
  const updateUserData = async (updates: UserDataUpdates): Promise<{ success: boolean }> => {
    // Validate updates
    const validationResult = validateUserInput(userProfileSchema, updates);
    if (!validationResult.success) {
      return { success: false };
    }
    
    // Apply updates
    const { error } = await supabase
      .from('user_profiles')
      .update(validationResult.data)
      .eq('user_id', userId);
      
    if (error) {
      return { success: false };
    }
    
    return { success: true };
  };
  
  return {
    canExportData: isAdmin || userId === supabase.auth.user()?.id,
    canDeleteData: isAdmin || userId === supabase.auth.user()?.id,
    exportUserData,
    deleteUserData,
    updateUserData
  };
};
```

### PCI-DSS Compliance

For payment processing, we implement PCI-DSS controls:

```typescript
// Configuration for PCI-DSS requirements
const pciDssControls = {
  // Requirement 1: Install and maintain a firewall configuration
  networkSegmentation: {
    paymentProcessingSubnet: 'isolated',
    firewallRules: 'restrictive'
  },
  
  // Requirement 2: Do not use vendor-supplied defaults
  passwordPolicy: {
    minLength: 12,
    complexity: true,
    expiration: 90, // days
    history: 10 // remember previous passwords
  },
  
  // Requirement 3: Protect stored cardholder data
  dataProtection: {
    storeCardData: false, // We don't store card data, use token-based system
    useTokenization: true,
    provider: 'stripe'
  },
  
  // Requirement 4: Encrypt transmission of cardholder data
  encryption: {
    inTransit: 'TLS_1_2_OR_HIGHER',
    atRest: 'AES_256'
  },
  
  // Requirement 5-12 implementations
  // ...
};

// Implementation in payment processing
const processPaymentSecurely = (paymentDetails) => {
  // Use Stripe Elements to avoid handling card data directly
  const stripe = await loadStripe(process.env.REACT_APP_STRIPE_PUBLIC_KEY);
  const elements = stripe.elements();
  
  // Complete payment with tokenized data only
  // We never see or handle actual card data in our application
};
```

### CCPA Compliance

Implementation of CCPA requirements:

```typescript
// src/features/privacy/CcpaPrivacyControls.tsx
const CcpaPrivacyControls: React.FC = () => {
  const { user } = useAuth();
  const [dataCollectionEnabled, setDataCollectionEnabled] = useState(true);
  const [dataSharingEnabled, setDataSharingEnabled] = useState(true);
  
  useEffect(() => {
    // Load user's current privacy preferences
    const loadPreferences = async () => {
      const { data } = await supabase
        .from('user_privacy_settings')
        .select('data_collection_enabled, data_sharing_enabled')
        .eq('user_id', user.id)
        .single();
        
      if (data) {
        setDataCollectionEnabled(data.data_collection_enabled);
        setDataSharingEnabled(data.data_sharing_enabled);
      }
    };
    
    if (user) loadPreferences();
  }, [user]);
  
  const updatePrivacySettings = async () => {
    // Update user's privacy preferences
    const { error } = await supabase
      .from('user_privacy_settings')
      .upsert({
        user_id: user.id,
        data_collection_enabled: dataCollectionEnabled,
        data_sharing_enabled: dataSharingEnabled,
        updated_at: new Date().toISOString()
      });
      
    // Log this privacy change
    await logAuditEvent(
      dataCollectionEnabled ? AuditEventType.PRIVACY_OPT_IN : AuditEventType.PRIVACY_OPT_OUT,
      {
        userId: user.id,
        resourceType: 'privacy_settings',
        resourceId: user.id,
        action: 'update',
        status: error ? 'failure' : 'success',
        metadata: { 
          data_collection: dataCollectionEnabled, 
          data_sharing: dataSharingEnabled 
        }
      }
    );
      
    // Update tracking settings
    updateTrackingConsent({
      analytics: dataCollectionEnabled,
      marketing: dataSharingEnabled
    });
  };
  
  return (
    <div className="privacy-controls">
      <h2>Your Privacy Choices</h2>
      
      <div className="control-group">
        <h3>Data Collection</h3>
        <p>
          Control how we collect data about your usage of the application. 
          This includes analytics and feature usage statistics.
        </p>
        <Toggle
          id="data-collection"
          checked={dataCollectionEnabled}
          onChange={setDataCollectionEnabled}
          label={dataCollectionEnabled ? "Data collection is ON" : "Data collection is OFF"}
        />
      </div>
      
      <div className="control-group">
        <h3>Data Sharing</h3>
        <p>
          Control whether your non-personal information may be shared with 
          tournament sponsors or partners.
        </p>
        <Toggle
          id="data-sharing"  
          checked={dataSharingEnabled}
          onChange={setDataSharingEnabled}
          label={dataSharingEnabled ? "Data sharing is ON" : "Data sharing is OFF"}
        />
      </div>
      
      <button 
        className="primary-button"
        onClick={updatePrivacySettings}>
        Save Privacy Choices
      </button>
      
      <div className="privacy-links">
        <a href="/privacy/export">Export My Data</a>
        <a href="/privacy/delete">Delete My Data</a>
      </div>
    </div>
  );
};
```

## Security Incident Response

Implementation of security incident response procedures:

```typescript
// src/lib/security-monitoring.ts
export enum SecurityIncidentSeverity {
  LOW = 'low',
  MEDIUM = 'medium',
  HIGH = 'high',
  CRITICAL = 'critical'
}

export interface SecurityIncident {
  type: string;
  severity: SecurityIncidentSeverity;
  description: string;
  affectedUserId?: string;
  affectedResource?: string;
  detectedAt: Date;
  metadata: Record<string, any>;
}

// Implement client-side security monitoring
export const monitorSecurityEvents = () => {
  // Monitor for suspicious authentication patterns
  let failedLoginAttempts = 0;
  let lastFailedLogin = new Date(0);
  
  // Listen for auth events
  supabase.auth.onAuthStateChange((event, session) => {
    const now = new Date();
    
    // Reset counter on successful login
    if (event === 'SIGNED_IN') {
      failedLoginAttempts = 0;
      return;
    }
    
    // Count failed logins
    if (event === 'USER_UPDATED' && !session) {
      failedLoginAttempts++;
      
      // Check for brute force pattern (5 failures within 10 minutes)
      const timeSinceLastFailure = now.getTime() - lastFailedLogin.getTime();
      if (failedLoginAttempts >= 5 && timeSinceLastFailure < 10 * 60 * 1000) {
        reportSecurityIncident({
          type: 'possible-brute-force',
          severity: SecurityIncidentSeverity.HIGH,
          description: 'Multiple failed login attempts detected',
          detectedAt: now,
          metadata: {
            attempts: failedLoginAttempts,
            timeWindow: timeSinceLastFailure
          }
        });
      }
      
      lastFailedLogin = now;
    }
  });
  
  // XSS attack detection
  const detectXssAttempts = () => {
    // Monitor for script injection attempts in URL
    const urlParams = new URLSearchParams(window.location.search);
    for (const [key, value] of urlParams.entries()) {
      if (
        value.includes('<script') || 
        value.includes('javascript:') ||
        value.includes('onerror=') ||
        value.includes('onload=')
      ) {
        reportSecurityIncident({
          type: 'possible-xss-attempt',
          severity: SecurityIncidentSeverity.MEDIUM,
          description: 'Possible XSS attempt detected in URL parameters',
          detectedAt: new Date(),
          metadata: {
            parameter: key,
            value: value
          }
        });
        
        // Sanitize URL by removing suspicious parameter
        urlParams.delete(key);
        const sanitizedUrl = `${window.location.pathname}?${urlParams.toString()}`;
        history.replaceState(null, '', sanitizedUrl);
      }
    }
  };
  
  // Run XSS detection on page load and navigation
  detectXssAttempts();
  window.addEventListener('popstate', detectXssAttempts);
};

export const reportSecurityIncident = async (incident: SecurityIncident) => {
  try {
    // Log incident to monitoring endpoint
    await fetch('/api/security/report-incident', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(incident)
    });
    
    // For high-severity incidents, also log to audit log
    if (
      incident.severity === SecurityIncidentSeverity.HIGH || 
      incident.severity === SecurityIncidentSeverity.CRITICAL
    ) {
      await logAuditEvent(AuditEventType.SECURITY_INCIDENT, {
        userId: supabase.auth.user()?.id || null,
        resourceType: 'security',
        resourceId: null,
        action: 'detect',
        status: 'success',
        metadata: { ...incident }
      });
    }
  } catch (error) {
    console.error('Failed to report security incident:', error);
  }
};
```

## Security Training and Awareness

Our security program includes regular developer training:

```typescript
// Security best practices documentation
const securityGuidelines = {
  authentication: [
    'Always use Supabase Auth for user authentication',
    'Never implement custom authentication bypasses',
    'Require strong passwords (min. 10 chars, mixed case, numbers, symbols)',
    'Implement proper account lockout after failed attempts',
    'Use MFA for all administrative functions'
  ],
  
  authorization: [
    'Always use RLS policies for data access control',
    'Never trust client-side role/permission checks alone',
    'Implement least privilege principle for all roles',
    'Require re-authentication for sensitive operations',
    'Validate permissions server-side for all API endpoints'
  ],
  
  dataProtection: [
    'Never store sensitive data in localStorage or sessionStorage',
    'Use column-level encryption for PII in database',
    'Never log sensitive data (PII, payment details, credentials)',
    'Use HTTPS for all API communications',
    'Implement proper data sanitization for all user inputs'
  ],
  
  securityTesting: [
    'Run SAST tools in CI/CD pipeline',
    'Perform regular penetration testing',
    'Implement security unit tests for critical functions',
    'Use dependency scanning for known vulnerabilities',
    'Conduct code reviews with security focus'
  ]
};
```

## Conclusion

Our comprehensive security and compliance strategy ensures that the Golf Tournament Organizer application protects user data, maintains compliance with relevant regulations, and follows industry best practices for secure application development. By implementing defense-in-depth across all application layers, we provide a secure platform for tournament organizers and participants.

The security measures outlined in this document form the foundation of our overall security posture, which is continuously monitored, tested, and improved to address emerging threats and comply with evolving regulations.
