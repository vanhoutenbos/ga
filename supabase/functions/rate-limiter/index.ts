// Supabase Edge Function for API rate limiting
// This function serves as a middleware for API rate limiting

import { createClient } from 'https://esm.sh/@supabase/supabase-js@2.38.0';

// Define rate limit configurations by endpoint type
const rateLimits = {
  default: {
    anonymous: { maxRequests: 60, windowSeconds: 60 }, // 1 req/sec for anonymous users
    authenticated: { maxRequests: 120, windowSeconds: 60 }, // 2 req/sec for authenticated users
  },
  leaderboards: {
    anonymous: { maxRequests: 30, windowSeconds: 60 }, // 0.5 req/sec for anonymous users
    authenticated: { maxRequests: 60, windowSeconds: 60 }, // 1 req/sec for authenticated users
  },
  scoring: {
    anonymous: { maxRequests: 0, windowSeconds: 60 }, // No access for anonymous users
    authenticated: { maxRequests: 120, windowSeconds: 60 }, // 2 req/sec for authenticated users
  }
};

// Helper to determine endpoint type from the request path
function getEndpointType(path) {
  if (path.includes('/leaderboard') || path.includes('/standings')) {
    return 'leaderboards';
  } else if (path.includes('/scores') || path.includes('/submit')) {
    return 'scoring';
  }
  return 'default';
}

// Helper to extract client IP address
function getClientIp(req) {
  return req.headers.get('x-forwarded-for')?.split(',')[0].trim() || 
         req.headers.get('cf-connecting-ip') || 
         req.headers.get('x-real-ip') ||
         '127.0.0.1';
}

export async function serve(req) {
  try {
    const url = new URL(req.url);
    const path = url.pathname;
    const endpointType = getEndpointType(path);
    const clientIp = getClientIp(req);
    
    // Create Supabase client
    const supabaseClient = createClient(
      Deno.env.get('SUPABASE_URL') ?? '',
      Deno.env.get('SUPABASE_ANON_KEY') ?? '',
      {
        global: { headers: { Authorization: req.headers.get('Authorization') ?? '' } },
        auth: { persistSession: false }
      }
    );
    
    // Get the current user if authenticated
    let userId = null;
    const authHeader = req.headers.get('Authorization');
    
    if (authHeader) {
      const { data: { user }, error } = await supabaseClient.auth.getUser();
      if (!error && user) {
        userId = user.id;
      }
    }
    
    // Determine the applicable rate limit
    const isAuthenticated = userId !== null;
    const applicableLimit = rateLimits[endpointType][isAuthenticated ? 'authenticated' : 'anonymous'];
    
    // Check rate limit using the database function
    const { data: isAllowed, error } = await supabaseClient.rpc('check_rate_limit', {
      ip_address_param: clientIp,
      user_id_param: userId,
      endpoint_param: endpointType,
      max_requests_param: applicableLimit.maxRequests,
      window_seconds_param: applicableLimit.windowSeconds
    });
    
    if (error) {
      console.error('Error checking rate limit:', error);
      // Default to allowing the request if the rate limit check fails
      return await fetch(req);
    }
    
    if (!isAllowed) {
      // Rate limit exceeded
      return new Response(
        JSON.stringify({
          error: 'Too Many Requests',
          message: 'Rate limit exceeded. Please try again later.'
        }),
        {
          status: 429,
          headers: {
            'Content-Type': 'application/json',
            'Retry-After': applicableLimit.windowSeconds.toString()
          }
        }
      );
    }
    
    // Rate limit not exceeded, forward the request
    return await fetch(req);
  } catch (error) {
    console.error('Rate limiting error:', error);
    // If something goes wrong with rate limiting, still allow the request
    return await fetch(req);
  }
}
