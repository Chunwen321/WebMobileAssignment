-- =============================================
-- Script to hash plain text password for U0005
-- =============================================
-- 
-- INSTRUCTIONS:
-- 1. The password "parent123" needs to be hashed
-- 2. Use the /Utility/HashPassword page to generate the hash
-- 3. Then run this script with the generated hash
-- 
-- HOW TO USE:
-- Step 1: Go to https://localhost:XXXX/Utility/HashPassword
-- Step 2: Enter "parent123" and click "Generate Hash"
-- Step 3: Copy the generated hash (starts with AQA...)
-- Step 4: Replace 'YOUR_GENERATED_HASH_HERE' below with the copied hash
-- Step 5: Run this script in your database
-- =============================================

USE [C:\USERS\LOWWE\SOURCE\REPOS\WEBMOBILEASSIGNMENT\WEBMOBILEASSIGNMENT\DB.MDF]
GO

-- Update the password for U0005
UPDATE Users 
SET PasswordHash = 'YOUR_GENERATED_HASH_HERE'
WHERE UserId = 'U0005';
GO

-- Verify the update
SELECT UserId, Email, PasswordHash, FullName
FROM Users
WHERE UserId = 'U0005';
GO

-- =============================================
-- Expected Result:
-- UserId: U0005
-- Email: lowwk-wm23@student.tarc.edu.my
-- PasswordHash: AQA... (should start with AQA and be very long)
-- FullName: halo
-- =============================================
