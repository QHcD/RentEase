-- =============================================
-- Property Leasing & Maintenance Platform
-- Seed Data Script (Test Data)
-- =============================================

USE PropertyLeasingDB;
GO

-- =============================================
-- Properties
-- =============================================
INSERT INTO Property (Name, Description, Address, City, PropertyType) VALUES
('Seef Tower', 'Modern residential tower near Seef Mall', 'Building 101, Seef District', 'Manama', 'Residential'),
('Gulf Business Center', 'Commercial offices in central Manama', 'Road 25, Diplomatic Area', 'Manama', 'Commercial'),
('Al Hidd Residences', 'Affordable apartments in Al Hidd', 'Block 12, Al Hidd', 'Muharraq', 'Residential');

-- =============================================
-- Units
-- =============================================
INSERT INTO Unit (PropertyID, UnitNumber, UnitType, Sizesqm, MonthlyRent, Amenities, AvailabilityStatus) VALUES
(1, '101', 'Apartment', 85.0,  450.00,  'Gym, Pool, Parking', 'Available'),
(1, '102', 'Studio',    45.0,  280.00,  'Parking', 'Available'),
(1, '201', 'Apartment', 90.0,  480.00,  'Gym, Pool, Parking, Balcony', 'Occupied'),
(2, 'G01', 'Office',    120.0, 800.00,  'Meeting Rooms, Parking', 'Available'),
(2, 'G02', 'Shop',      60.0,  600.00,  'Parking', 'Occupied'),
(3, 'A1',  'Apartment', 75.0,  350.00,  'Parking', 'Available'),
(3, 'A2',  'Apartment', 75.0,  350.00,  'Parking', 'UnderMaintenance');

-- =============================================
-- Users
-- =============================================
INSERT INTO [User] (FullName, Email, Phone, [Role], SkillProfile, AvailabilityStatus) VALUES
('Ahmed Al Mansoori',  'manager@propleasing.com',    '+973 3300 0001', 'PropertyManager',   NULL,                        NULL),
('Sara Al Khalifa',    'tenant1@example.com',         '+973 3300 0002', 'Tenant',            NULL,                        NULL),
('Mohammed Al Tajer',  'tenant2@example.com',         '+973 3300 0003', 'Tenant',            NULL,                        NULL),
('Ali Hassan',         'staff1@propleasing.com',      '+973 3300 0004', 'MaintenanceStaff',  'Plumbing, General',         'Available'),
('Yusuf Al Zayani',    'staff2@propleasing.com',      '+973 3300 0005', 'MaintenanceStaff',  'Electrical, HVAC',          'Available'),
('Noor Ibrahim',       'tenant3@example.com',         '+97333667788',   'Tenant',            NULL,                        NULL);

-- Lease applications, documents, and leases are seeded by ContextSeed (LeaseApplicationSeedData).
-- Set Seed:ResetBusinessDataOnStartup=true in development to reload demo data.

-- =============================================
-- Maintenance Requests
-- =============================================
INSERT INTO MaintenanceRequest (UnitID, TenantUserID, AssignedStaffID, Title, Description, RequestType, Priority, Status, TicketNumber, SubmittedAt) VALUES
(3, 3, 4, 'Leaking pipe in bathroom',    'Water dripping from under the sink',    'Plumbing',   'High',   'Assigned',  'TKT-2026-001', GETDATE()),
(5, 6, 5, 'AC not cooling',              'Air conditioner stopped working',        'HVAC',       'Medium', 'InProgress','TKT-2026-002', GETDATE()),
(7, 2, NULL, 'Broken door lock',         'Front door lock is jammed',              'General',    'Urgent', 'Submitted', 'TKT-2026-003', GETDATE());

-- =============================================
-- Maintenance Status History
-- =============================================
INSERT INTO MaintenanceStatusHistory (RequestID, OldStatus, NewStatus, Notes, ChangedAt, ChangedByUserID) VALUES
(1, 'Submitted', 'Assigned',   'Assigned to Ali Hassan',        GETDATE(), 1),
(2, 'Submitted', 'Assigned',   'Assigned to Yusuf Al Zayani',   GETDATE(), 1),
(2, 'Assigned',  'InProgress', 'Staff on site',                 GETDATE(), 5);

-- =============================================
-- Notifications
-- =============================================
INSERT INTO Notification (UserID, Message, NotificationType, Status) VALUES
(3, 'Your maintenance request TKT-2026-001 has been assigned to a technician.', 'MaintenanceUpdate', 'Read'),
(6, 'New maintenance request submitted: TKT-2026-003', 'MaintenanceUpdate','Unread');

-- =============================================
-- Logs
-- =============================================
INSERT INTO Log (UserID, Action, Details, LogLevel, Source) VALUES
(1, 'CreateLease',          'Lease created for ApplicationID=1',      'Info',    'Web'),
(1, 'ApproveApplication',   'Application ID=1 approved',              'Info',    'Web'),
(2, 'Login',                'Tenant login successful',                'Info',    'Web'),
(NULL, 'Error',             'Unhandled exception in HomeController',  'Error',   'Web');

PRINT 'Seed data inserted successfully!';
GO
