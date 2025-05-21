-- Migration Name: 20250521000000_caching_support
-- Description: Add caching support functions and tables
-- Created By: GitHub Copilot

BEGIN;

-- Create a table to track cache invalidation events
CREATE TABLE cache_invalidation_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    resource_type VARCHAR(100) NOT NULL,
    resource_id UUID,
    tenant_id UUID REFERENCES tenants(id),
    invalidated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID REFERENCES auth.users(id)
);

-- Enable RLS for the cache invalidation table
ALTER TABLE cache_invalidation_events ENABLE ROW LEVEL SECURITY;

-- Create a policy allowing users to see cache events in their tenant
CREATE POLICY cache_events_tenant_policy ON cache_invalidation_events
    FOR SELECT
    USING (
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid()
        )
    );

-- Create a policy allowing authenticated users to create invalidation events
CREATE POLICY cache_events_insert_policy ON cache_invalidation_events
    FOR INSERT
    WITH CHECK (
        -- Users can create invalidation events for their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid()
        )
    );

-- Create a function to invalidate cache for a specific resource
CREATE OR REPLACE FUNCTION invalidate_cache(
    resource_type_param VARCHAR,
    resource_id_param UUID,
    tenant_id_param UUID
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO cache_invalidation_events (resource_type, resource_id, tenant_id, created_by)
    VALUES (resource_type_param, resource_id_param, tenant_id_param, auth.uid());
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Create a function to get the latest cache invalidation timestamp for a resource
CREATE OR REPLACE FUNCTION get_cache_timestamp(
    resource_type_param VARCHAR,
    resource_id_param UUID DEFAULT NULL,
    tenant_id_param UUID DEFAULT NULL
)
RETURNS TIMESTAMPTZ AS $$
DECLARE
    result TIMESTAMPTZ;
BEGIN
    -- Get the most recent invalidation event for this resource type/id/tenant
    SELECT MAX(invalidated_at) INTO result
    FROM cache_invalidation_events
    WHERE resource_type = resource_type_param
    AND (resource_id = resource_id_param OR resource_id_param IS NULL)
    AND (tenant_id = tenant_id_param OR tenant_id_param IS NULL);
    
    -- If no invalidation event is found, return epoch
    IF result IS NULL THEN
        RETURN '1970-01-01 00:00:00+00'::TIMESTAMPTZ;
    END IF;
    
    RETURN result;
END;
$$ LANGUAGE plpgsql SECURITY INVOKER;

-- Create triggers to automatically invalidate cache when data changes

-- Tournaments table trigger
CREATE OR REPLACE FUNCTION invalidate_tournament_cache()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM invalidate_cache('tournament', NEW.id, NEW.tenant_id);
    PERFORM invalidate_cache('tournaments_list', NULL, NEW.tenant_id);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER tournament_cache_invalidation
    AFTER INSERT OR UPDATE ON tournaments
    FOR EACH ROW EXECUTE FUNCTION invalidate_tournament_cache();

-- Scores table trigger
CREATE OR REPLACE FUNCTION invalidate_score_cache()
RETURNS TRIGGER AS $$
DECLARE
    tournament_tenant_id UUID;
BEGIN
    -- Get the tournament's tenant_id
    SELECT tenant_id INTO tournament_tenant_id
    FROM tournaments
    WHERE id = NEW.tournament_id;
    
    -- Invalidate specific caches
    PERFORM invalidate_cache('tournament_scores', NEW.tournament_id, tournament_tenant_id);
    PERFORM invalidate_cache('tournament_leaderboard', NEW.tournament_id, tournament_tenant_id);
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER score_cache_invalidation
    AFTER INSERT OR UPDATE ON scores
    FOR EACH ROW EXECUTE FUNCTION invalidate_score_cache();

-- Create a rate limiting table
CREATE TABLE rate_limits (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    ip_address VARCHAR(100),
    user_id UUID REFERENCES auth.users(id),
    endpoint VARCHAR(255) NOT NULL,
    request_count INTEGER NOT NULL DEFAULT 1,
    first_request_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_request_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reset_at TIMESTAMPTZ NOT NULL,
    UNIQUE(ip_address, endpoint, user_id)
);

-- Enable RLS for the rate limiting table
ALTER TABLE rate_limits ENABLE ROW LEVEL SECURITY;

-- Create a policy allowing only admins to view rate limit data
CREATE POLICY rate_limits_admin_policy ON rate_limits
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
            FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        )
    );

-- Create a function to check and update rate limits
CREATE OR REPLACE FUNCTION check_rate_limit(
    ip_address_param VARCHAR,
    user_id_param UUID,
    endpoint_param VARCHAR,
    max_requests_param INTEGER,
    window_seconds_param INTEGER
)
RETURNS BOOLEAN AS $$
DECLARE
    current_count INTEGER;
    reset_time TIMESTAMPTZ;
    limit_record RECORD;
BEGIN
    -- Calculate the reset time
    SELECT NOW() + (window_seconds_param || ' seconds')::INTERVAL INTO reset_time;
    
    -- Check if a rate limit record already exists
    SELECT * INTO limit_record
    FROM rate_limits
    WHERE (ip_address = ip_address_param OR (ip_address IS NULL AND ip_address_param IS NULL))
    AND (user_id = user_id_param OR (user_id IS NULL AND user_id_param IS NULL))
    AND endpoint = endpoint_param;
    
    IF limit_record IS NULL THEN
        -- No existing record, create a new one
        INSERT INTO rate_limits (
            ip_address, user_id, endpoint, request_count, 
            first_request_at, last_request_at, reset_at
        )
        VALUES (
            ip_address_param, user_id_param, endpoint_param, 1,
            NOW(), NOW(), reset_time
        );
        
        -- First request is always allowed
        RETURN TRUE;
    ELSE
        -- Check if we need to reset the counter (window expired)
        IF NOW() > limit_record.reset_at THEN
            -- Reset the counter
            UPDATE rate_limits
            SET request_count = 1,
                first_request_at = NOW(),
                last_request_at = NOW(),
                reset_at = reset_time
            WHERE id = limit_record.id;
            
            -- Request allowed (first in new window)
            RETURN TRUE;
        ELSE
            -- Check if we've exceeded the limit
            IF limit_record.request_count >= max_requests_param THEN
                -- Rate limit exceeded
                RETURN FALSE;
            ELSE
                -- Increment the counter
                UPDATE rate_limits
                SET request_count = request_count + 1,
                    last_request_at = NOW()
                WHERE id = limit_record.id;
                
                -- Request allowed
                RETURN TRUE;
            END IF;
        END IF;
    END IF;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

COMMIT;
