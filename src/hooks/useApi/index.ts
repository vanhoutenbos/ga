// This hook implements an API client with retry and circuit breaker patterns
// for resilient API calls in the frontend

import { useState, useCallback, useEffect, useRef } from 'react';
import axios, { AxiosRequestConfig, AxiosResponse, AxiosError } from 'axios';
import { useAuth } from '../../contexts/AuthContext';
import { useOffline } from '../../contexts/OfflineContext';

// Default configuration
const DEFAULT_RETRY_COUNT = 3;
const DEFAULT_RETRY_DELAY = 1000; // Start with 1s delay
const DEFAULT_BACKOFF_FACTOR = 2; // Exponential backoff factor
const DEFAULT_STATUS_CODES_TO_RETRY = [408, 429, 500, 502, 503, 504];
const DEFAULT_CIRCUIT_BREAKER_THRESHOLD = 5; // Number of failures before circuit opens
const DEFAULT_CIRCUIT_BREAKER_TIMEOUT = 30 * 1000; // 30 seconds circuit timeout

// Circuit breaker state
type CircuitState = 'CLOSED' | 'OPEN' | 'HALF_OPEN';

// Circuit breaker state per endpoint
interface CircuitBreakerState {
  state: CircuitState;
  failures: number;
  lastFailureTime: number | null;
  endpoints: Record<string, {
    state: CircuitState;
    failures: number;
    lastFailureTime: number | null;
  }>;
}

// Initial circuit breaker state
const initialCircuitBreakerState: CircuitBreakerState = {
  state: 'CLOSED',
  failures: 0,
  lastFailureTime: null,
  endpoints: {}
};

// Singleton to maintain circuit breaker state across hook instances
let globalCircuitBreakerState = { ...initialCircuitBreakerState };

// Constants for local storage
const QUEUED_REQUESTS_KEY = 'golfapp_queued_requests';

export interface UseApiOptions {
  baseURL?: string;
  retryCount?: number;
  retryDelay?: number;
  backoffFactor?: number;
  statusCodesToRetry?: number[];
  circuitBreakerThreshold?: number;
  circuitBreakerTimeout?: number;
}

export const useApi = (options: UseApiOptions = {}) => {
  // Destructure options with defaults
  const {
    baseURL = process.env.REACT_APP_API_URL || 'https://api.golfapp.com',
    retryCount = DEFAULT_RETRY_COUNT,
    retryDelay = DEFAULT_RETRY_DELAY,
    backoffFactor = DEFAULT_BACKOFF_FACTOR,
    statusCodesToRetry = DEFAULT_STATUS_CODES_TO_RETRY,
    circuitBreakerThreshold = DEFAULT_CIRCUIT_BREAKER_THRESHOLD,
    circuitBreakerTimeout = DEFAULT_CIRCUIT_BREAKER_TIMEOUT,
  } = options;

  // Get auth and offline context
  const { session } = useAuth();
  const { isOnline, setSyncRequired } = useOffline();
  
  // Reference to the axios instance
  const apiClientRef = useRef(axios.create({
    baseURL,
    headers: {
      'Content-Type': 'application/json',
    },
  }));

  // State for loading and errors
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  // Function to add auth token to requests
  useEffect(() => {
    const requestInterceptor = apiClientRef.current.interceptors.request.use(
      async (config) => {
        // Add auth token if available
        if (session?.access_token) {
          config.headers.Authorization = `Bearer ${session.access_token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Cleanup
    return () => {
      apiClientRef.current.interceptors.request.eject(requestInterceptor);
    };
  }, [session]);

  // Check if we should attempt a retry
  const shouldRetry = useCallback(
    (error: AxiosError, retryCount: number): boolean => {
      // Don't retry if we've hit the max retry count
      if (retryCount <= 0) return false;

      // Don't retry if we're offline
      if (!isOnline) return false;

      // Don't retry if it's a client error (except for the ones we specify)
      if (error.response) {
        return statusCodesToRetry.includes(error.response.status);
      }

      // Retry network errors (i.e., no response)
      return true;
    },
    [isOnline, statusCodesToRetry]
  );

  // Check if the circuit breaker is open for a specific endpoint
  const isCircuitOpen = useCallback((endpoint: string): boolean => {
    const now = Date.now();
    const globalCircuit = globalCircuitBreakerState;
    const endpointCircuit = globalCircuit.endpoints[endpoint] || {
      state: 'CLOSED',
      failures: 0,
      lastFailureTime: null,
    };

    // If circuit is OPEN, check if timeout has elapsed to transition to HALF_OPEN
    if (endpointCircuit.state === 'OPEN') {
      if (endpointCircuit.lastFailureTime && now - endpointCircuit.lastFailureTime > circuitBreakerTimeout) {
        endpointCircuit.state = 'HALF_OPEN';
        globalCircuit.endpoints[endpoint] = endpointCircuit;
        return false;
      }
      return true;
    }

    return false;
  }, [circuitBreakerTimeout]);

  // Update circuit breaker state after a request
  const updateCircuitBreaker = useCallback((endpoint: string, isSuccess: boolean) => {
    const globalCircuit = globalCircuitBreakerState;
    const endpointCircuit = globalCircuit.endpoints[endpoint] || {
      state: 'CLOSED',
      failures: 0,
      lastFailureTime: null,
    };

    if (isSuccess) {
      // On success, reset circuit if it was HALF_OPEN
      if (endpointCircuit.state === 'HALF_OPEN') {
        endpointCircuit.state = 'CLOSED';
        endpointCircuit.failures = 0;
        endpointCircuit.lastFailureTime = null;
      }
    } else {
      // On failure, increment failure count
      endpointCircuit.failures += 1;
      endpointCircuit.lastFailureTime = Date.now();

      // If we've hit the threshold, open the circuit
      if (endpointCircuit.failures >= circuitBreakerThreshold) {
        endpointCircuit.state = 'OPEN';
      }
    }

    // Update global state
    globalCircuit.endpoints[endpoint] = endpointCircuit;
  }, [circuitBreakerThreshold]);

  // Queue a request for when we're back online
  const queueRequest = useCallback((method: string, url: string, data?: any, config?: AxiosRequestConfig) => {
    try {
      const queuedRequests = JSON.parse(localStorage.getItem(QUEUED_REQUESTS_KEY) || '[]');
      queuedRequests.push({
        method,
        url,
        data,
        config,
        timestamp: Date.now()
      });
      localStorage.setItem(QUEUED_REQUESTS_KEY, JSON.stringify(queuedRequests));
      setSyncRequired(true);
    } catch (error) {
      console.error('Error queueing request for offline sync:', error);
    }
  }, [setSyncRequired]);

  // Main request function with retry logic
  const request = useCallback(async <T>(
    method: string,
    url: string,
    data?: any,
    config?: AxiosRequestConfig
  ): Promise<AxiosResponse<T>> => {
    // Use the relative URL (without baseURL) as the endpoint identifier for circuit breaking
    const endpoint = url.startsWith('/') ? url : `/${url}`;
    
    // Check if circuit is open for this endpoint
    if (isCircuitOpen(endpoint)) {
      throw new Error(`Circuit is open for ${endpoint}. Too many failures.`);
    }

    // Handle offline mode for mutations
    if (!isOnline && ['post', 'put', 'patch', 'delete'].includes(method.toLowerCase())) {
      // Queue the request for later if it's a mutation
      queueRequest(method, url, data, config);
      throw new Error(`You are offline. The request has been queued for when you're back online.`);
    }

    let attempt = 0;
    setIsLoading(true);
    setError(null);

    const executeRequest = async (): Promise<AxiosResponse<T>> => {
      try {
        attempt++;
        let response: AxiosResponse<T>;

        switch (method.toLowerCase()) {
          case 'get':
            response = await apiClientRef.current.get<T>(url, config);
            break;
          case 'post':
            response = await apiClientRef.current.post<T>(url, data, config);
            break;
          case 'put':
            response = await apiClientRef.current.put<T>(url, data, config);
            break;
          case 'patch':
            response = await apiClientRef.current.patch<T>(url, data, config);
            break;
          case 'delete':
            response = await apiClientRef.current.delete<T>(url, config);
            break;
          default:
            throw new Error(`Unsupported HTTP method: ${method}`);
        }

        // Success! Update circuit breaker and return response
        updateCircuitBreaker(endpoint, true);
        return response;
      } catch (err) {
        const error = err as AxiosError;
        
        // Update circuit breaker on failure
        updateCircuitBreaker(endpoint, false);

        // Handle 429 rate limit with proper backoff using the Retry-After header
        if (error.response?.status === 429) {
          const retryAfter = error.response.headers['retry-after'];
          const retryDelayMs = retryAfter ? parseInt(retryAfter) * 1000 : retryDelay * Math.pow(backoffFactor, attempt - 1);
          
          if (shouldRetry(error, retryCount - attempt + 1)) {
            console.warn(`Rate limited. Retrying after ${retryDelayMs}ms...`);
            await new Promise(resolve => setTimeout(resolve, retryDelayMs));
            return executeRequest();
          }
        }
        // Regular retry logic for other errors
        else if (shouldRetry(error, retryCount - attempt + 1)) {
          const calculatedDelay = retryDelay * Math.pow(backoffFactor, attempt - 1);
          console.warn(`Request failed. Retrying (${attempt}/${retryCount}) after ${calculatedDelay}ms...`);
          await new Promise(resolve => setTimeout(resolve, calculatedDelay));
          return executeRequest();
        }

        // If we shouldn't retry or have exhausted retries, throw the error
        throw error;
      }
    };

    try {
      const response = await executeRequest();
      setIsLoading(false);
      return response;
    } catch (err) {
      setIsLoading(false);
      setError(err as Error);
      throw err;
    }
  }, [
    isOnline,
    queueRequest,
    retryCount,
    retryDelay,
    backoffFactor,
    shouldRetry,
    isCircuitOpen,
    updateCircuitBreaker
  ]);

  // Convenience methods for common HTTP methods
  const get = useCallback(<T>(url: string, config?: AxiosRequestConfig) => {
    return request<T>('get', url, undefined, config);
  }, [request]);

  const post = useCallback(<T>(url: string, data?: any, config?: AxiosRequestConfig) => {
    return request<T>('post', url, data, config);
  }, [request]);

  const put = useCallback(<T>(url: string, data?: any, config?: AxiosRequestConfig) => {
    return request<T>('put', url, data, config);
  }, [request]);

  const patch = useCallback(<T>(url: string, data?: any, config?: AxiosRequestConfig) => {
    return request<T>('patch', url, data, config);
  }, [request]);

  const del = useCallback(<T>(url: string, config?: AxiosRequestConfig) => {
    return request<T>('delete', url, undefined, config);
  }, [request]);

  return {
    get,
    post,
    put,
    patch,
    delete: del,
    request,
    isLoading,
    error,
    clearError: () => setError(null),
  };
};

export default useApi;
