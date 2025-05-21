-- Migration Name: 20250502000000_row_level_security_policies
-- Description: Row Level Security (RLS) policies for all tables
-- Created By: CI/CD Pipeline

BEGIN;

-- Tenants RLS Policies
CREATE POLICY tenant_select_policy ON tenants
    FOR SELECT
    USING (
        -- Admins can see all tenants
        is_admin() OR
        -- Users can see tenants they belong to
        id IN (SELECT tenant_id FROM tenant_users WHERE user_id = auth.uid())
    );

CREATE POLICY tenant_insert_policy ON tenants
    FOR INSERT
    WITH CHECK (
        -- Only admins can create tenants
        is_admin()
    );

CREATE POLICY tenant_update_policy ON tenants
    FOR UPDATE
    USING (
        -- Admins can update all tenants
        is_admin() OR
        -- Tenant admins can update their tenant
        id IN (SELECT tenant_id FROM tenant_users WHERE user_id = auth.uid() AND role = 'admin')
    );

CREATE POLICY tenant_delete_policy ON tenants
    FOR DELETE
    USING (
        -- Only super admins can delete tenants
        is_admin()
    );

-- Profiles RLS Policies
CREATE POLICY profile_select_policy ON profiles
    FOR SELECT
    USING (
        -- Users can see their own profile
        id = auth.uid() OR
        -- Users can see profiles of users in the same tenant
        id IN (
            SELECT user_id FROM tenant_users tu1
            WHERE EXISTS (
                SELECT 1 FROM tenant_users tu2
                WHERE tu2.user_id = auth.uid()
                AND tu1.tenant_id = tu2.tenant_id
            )
        )
    );

CREATE POLICY profile_insert_policy ON profiles
    FOR INSERT
    WITH CHECK (
        -- Users can insert their own profile
        id = auth.uid()
    );

CREATE POLICY profile_update_policy ON profiles
    FOR UPDATE
    USING (
        -- Users can update their own profile
        id = auth.uid()
    );

-- Tenant Users RLS Policies
CREATE POLICY tenant_users_select_policy ON tenant_users
    FOR SELECT
    USING (
        -- Users can see their own tenant associations
        user_id = auth.uid() OR
        -- Tenant admins can see all users in their tenants
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        )
    );

CREATE POLICY tenant_users_insert_policy ON tenant_users
    FOR INSERT
    WITH CHECK (
        -- Tenant admins can add users to their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        ) OR
        -- Global admins can add users to any tenant
        is_admin()
    );

CREATE POLICY tenant_users_update_policy ON tenant_users
    FOR UPDATE
    USING (
        -- Tenant admins can update users in their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        ) OR
        -- Global admins can update any tenant user
        is_admin()
    );

CREATE POLICY tenant_users_delete_policy ON tenant_users
    FOR DELETE
    USING (
        -- Tenant admins can remove users from their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        ) OR
        -- Global admins can remove any tenant user
        is_admin()
    );

-- Courses RLS Policies
CREATE POLICY courses_select_policy ON courses
    FOR SELECT
    USING (
        -- Public courses are visible to all
        is_public OR
        -- Users can see courses in their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid()
        )
    );

CREATE POLICY courses_insert_policy ON courses
    FOR INSERT
    WITH CHECK (
        -- Organizers and admins can add courses to their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role IN ('admin', 'organizer')
        )
    );

CREATE POLICY courses_update_policy ON courses
    FOR UPDATE
    USING (
        -- Course creators can update their courses
        created_by = auth.uid() OR
        -- Organizers and admins can update courses in their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role IN ('admin', 'organizer')
        )
    );

CREATE POLICY courses_delete_policy ON courses
    FOR DELETE
    USING (
        -- Course creators can delete their courses
        created_by = auth.uid() OR
        -- Tenant admins can delete courses in their tenant
        tenant_id IN (
            SELECT tenant_id FROM tenant_users
            WHERE user_id = auth.uid() AND role = 'admin'
        )
    );

-- Tees RLS Policies
CREATE POLICY tees_select_policy ON tees
    FOR SELECT
    USING (
        -- Users can see tees for courses they can access
        course_id IN (
            SELECT id FROM courses
            WHERE is_public OR
            tenant_id IN (
                SELECT tenant_id FROM tenant_users
                WHERE user_id = auth.uid()
            )
        )
    );

CREATE POLICY tees_insert_policy ON tees
    FOR INSERT
    WITH CHECK (
        -- Organizers and admins can add tees to courses in their tenant
        course_id IN (
            SELECT id FROM courses
            WHERE tenant_id IN (
                SELECT tenant_id FROM tenant_users
                WHERE user_id = auth.uid() AND role IN ('admin', 'organizer')
            )
        )
    );

CREATE POLICY tees_update_policy ON tees
    FOR UPDATE
    USING (
        -- Organizers and admins can update tees for courses in their tenant
        course_id IN (
            SELECT id FROM courses
            WHERE tenant_id IN (
                SELECT tenant_id FROM tenant_users
                WHERE user_id = auth.uid() AND role IN ('admin', 'organizer')
            )
        )
    );

CREATE POLICY tees_delete_policy ON tees
    FOR DELETE
    USING (
        -- Organizers and admins can delete tees for courses in their tenant
        course_id IN (
            SELECT id FROM courses
            WHERE tenant_id IN (
                SELECT tenant_id FROM tenant_users
                WHERE user_id = auth.uid() AND role IN ('admin', 'organizer')
            )
        )
    );

-- Similar policies for other tables...
-- Holes, Tournaments, Flights, Tournament Players, Scores, Tee Times, Tee Time Players

-- Helper function for admin check
CREATE OR REPLACE FUNCTION is_admin()
RETURNS BOOLEAN AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1
        FROM tenant_users
        WHERE user_id = auth.uid() AND role = 'admin'
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

COMMIT;
