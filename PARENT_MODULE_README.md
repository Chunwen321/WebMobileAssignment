# Parent Portal Module

## Overview
A complete parent portal module for the Tuition Attendance System with a sidebar navigation and comprehensive features.

## Features Implemented

### 1. Dashboard (`/Parent/Dashboard`)
- Child summary card with photo and basic info
- Attendance statistics (Present, Absent, Late, Attendance Rate)
- Attendance overview chart placeholder
- Recent attendance table (last 5 records)

### 2. Attendance Management

#### 2.1 View Attendance History (`/Parent/AttendanceHistory`)
- Filter by class, month, and status (Present/Absent/Late)
- Summary cards showing total present, absent, late, and attendance rate
- Detailed attendance table with date, class, time in/out, status, and remarks
- Export to Excel and PDF options
- Pagination support

#### 2.2 Monthly Summary (`/Parent/MonthlySummary`)
- Monthly statistics overview
- Attendance percentage visualization (pie chart placeholder)
- Monthly trend chart (line chart placeholder)
- Detailed summary by subject with progress bars
- Print and export functionality

#### 2.3 Download Attendance Report (`/Parent/DownloadReport`)
- Currently redirects to Monthly Summary
- Ready for PDF generation implementation

### 3. Child Profile (`/Parent/StudentProfile`)
- Student photo and basic information
- Class information (current class, schedule, time, teacher)
- Attendance summary with statistics
- Guardian contact information (primary and secondary)
- Emergency contact details

### 4. Notifications (`/Parent/Notifications`)
- Notification statistics dashboard
- Filter tabs (All, Unread, Alerts, Announcements)
- Absence alerts
- Consecutive late alerts
- Important announcements
- Achievement notifications
- Read/Unread status with badges
- Pagination support

### 5. Settings (`/Parent/Settings`)
- Account settings (name, email, phone, address)
- Change password functionality
- Notification preferences (Email, SMS, Alerts)
- Quick actions sidebar
- Security tips
- Account deactivation and deletion options

## Sidebar Navigation
The sidebar features:
- **Yellow theme (#ffc107)** matching the parent icon from login page
- Responsive design with mobile toggle
- Active menu highlighting
- Dropdown menu for Attendance submenu
- Notification badge showing unread count
- User profile footer with logout option

### Sidebar Menu Structure:
1. **Dashboard** - Overview and statistics
2. **Attendance** (Dropdown)
   - View Attendance History
   - Monthly Summary
   - Download Report (PDF)
3. **Child Profile** - Student information
4. **Notifications** - Alerts and messages (with badge)
5. **Settings** - Account and preferences

## Design Features

### Color Scheme
- **Primary Sidebar Color**: Dark gradient (#2c3e50 to #34495e)
- **Accent Color**: Yellow (#ffc107) - matches login page parent icon
- **Success**: #198754
- **Danger**: #dc3545
- **Warning**: #ffc107
- **Info**: #0d6efd

### Responsive Design
- Desktop: Full sidebar (280px width)
- Tablet/Mobile: Collapsible sidebar with overlay
- Mobile-first approach
- Touch-friendly navigation

### Components
- Dashboard cards with hover effects
- Stat cards with colored borders
- Notification items with different types (danger, warning, info, success)
- Progress bars for attendance rates
- Badges for status indicators
- Modal dialogs for confirmations

## Files Created

### Controllers
- `Controllers/ParentController.cs` - All parent routes and actions

### Views
- `Views/Shared/_ParentLayout.cshtml` - Parent-specific layout with sidebar
- `Views/Parent/_ViewStart.cshtml` - Auto-applies parent layout
- `Views/Parent/Dashboard.cshtml` - Main dashboard
- `Views/Parent/AttendanceHistory.cshtml` - Attendance history view
- `Views/Parent/MonthlySummary.cshtml` - Monthly attendance summary
- `Views/Parent/StudentProfile.cshtml` - Child profile page
- `Views/Parent/Notifications.cshtml` - Notifications center
- `Views/Parent/Settings.cshtml` - Settings page

### Assets
- `wwwroot/css/parent.css` - All parent portal styles
- `wwwroot/js/parent.js` - Sidebar toggle and interactions

### Updated Files
- `Controllers/AccountController.cs` - Added parent login redirect
- `Views/Account/Login.cshtml` - Form submission to controller

## How to Access

1. Navigate to `/Account/Login`
2. Select "Parent/Guardian" as user type
3. Enter any username and password (placeholder authentication)
4. Click "Login"
5. You'll be redirected to `/Parent/Dashboard`

## Future Enhancements

### To Implement:
1. **PDF Generation** - Implement attendance report PDF download
2. **Chart Integration** - Add Chart.js for attendance visualizations
3. **Real Data** - Connect to database and display actual student data
4. **Authentication** - Implement proper authentication and session management
5. **Email Notifications** - Send email alerts for absence/late
6. **SMS Integration** - SMS notifications for important alerts
7. **Multi-Child Support** - Allow parents with multiple children
8. **Calendar View** - Visual calendar showing attendance
9. **Export Functionality** - Excel and PDF export implementation
10. **Real-time Updates** - WebSocket for live notifications

## Technical Stack
- **Framework**: ASP.NET Core 9.0 MVC (Razor Pages compatible)
- **CSS Framework**: Bootstrap 5.3.2
- **Icons**: Bootstrap Icons 1.11.0
- **JavaScript**: Vanilla JS (no dependencies)
- **Layout**: Flexbox and CSS Grid

## Browser Support
- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Notes
- All data is currently placeholder/mock data
- Authentication is basic redirect (no security implemented)
- PDF download redirects to Monthly Summary (implementation pending)
- Charts are placeholders (need Chart.js integration)
- Responsive breakpoints: 576px, 768px, 992px
