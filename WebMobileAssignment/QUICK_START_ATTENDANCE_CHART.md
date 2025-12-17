# Quick Start: Using the Attendance Chart Function

## For Your Friend/Team Member

Hi! Here's a simple guide to use the attendance visualization chart that's now available in the project.

## What You Need

1. A canvas element in your HTML/Razor view
2. Include Chart.js CDN
3. Include the parent.js file
4. Call the `renderAttendanceChart()` function

## Simple 3-Step Setup

### Step 1: Add This to Your View (HTML)

```html
<div style="padding: 28px; background: #f8f9fa; border-radius: 8px; position: relative;">
    <canvas id="attendanceOverviewChart" style="max-height: 300px;"></canvas>
</div>
```

### Step 2: Add Chart.js at the Bottom of Your View

```html
@section Scripts {
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
}
```

### Step 3: Call the Function

Add this script below the Chart.js include:

```html
@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
    
    <script>
      // Wait for page to load
        document.addEventListener('DOMContentLoaded', function() {
            // Call the function with your data
         renderAttendanceChart('attendanceOverviewChart', {
     presentCount: 45,   // Replace with your present count
                absentCount: 3,     // Replace with your absent count
                lateCount: 2,       // Replace with your late count
        totalAttendance: 50 // Replace with your total
         });
 });
    </script>
}
```

## Using Data from Controller (ViewBag)

If your controller passes data via ViewBag:

```csharp
// In your Controller
ViewBag.PresentCount = 45;
ViewBag.AbsentCount = 3;
ViewBag.LateCount = 2;
ViewBag.TotalAttendance = 50;
```

Then in your View:

```html
@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
    
    <script>
      document.addEventListener('DOMContentLoaded', function() {
      renderAttendanceChart('attendanceOverviewChart', {
 presentCount: @(ViewBag.PresentCount ?? 0),
 absentCount: @(ViewBag.AbsentCount ?? 0),
                lateCount: @(ViewBag.LateCount ?? 0),
         totalAttendance: @(ViewBag.TotalAttendance ?? 0)
     });
        });
    </script>
}
```

## Auto-Initialize (Even Easier!)

Just add data attributes to the canvas:

```html
<canvas id="attendanceOverviewChart" 
  style="max-height: 300px;"
 data-present="@(ViewBag.PresentCount ?? 0)"
        data-absent="@(ViewBag.AbsentCount ?? 0)"
        data-late="@(ViewBag.LateCount ?? 0)"
        data-total="@(ViewBag.TotalAttendance ?? 0)">
</canvas>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
    <!-- No extra script needed! It auto-initializes -->
}
```

## Updating the Chart (For AJAX/Dynamic Updates)

If you need to update the chart when data changes:

```javascript
// Just call the function again with new data
renderAttendanceChart('attendanceOverviewChart', {
  presentCount: newPresentCount,
    absentCount: newAbsentCount,
    lateCount: newLateCount,
    totalAttendance: newTotal
});
```

## Common Issues & Solutions

### 1. Chart Not Showing
- **Check**: Did you include Chart.js CDN?
- **Check**: Did you include parent.js?
- **Check**: Is the canvas ID correct?
- **Check**: Open browser console (F12) for errors

### 2. "renderAttendanceChart is not defined"
- **Solution**: Make sure you included `<script src="~/js/parent.js"></script>`

### 3. Chart Shows but Data is Wrong
- **Solution**: Check your data values in the console:
```javascript
console.log('Present:', presentCount);
console.log('Absent:', absentCount);
console.log('Late:', lateCount);
```

## Examples You Can Copy

### Example 1: Basic Static Chart
```html
<div class="dashboard-card">
    <h3>Attendance Overview</h3>
    <div style="padding: 28px;">
    <canvas id="attendanceOverviewChart" style="max-height: 300px;"></canvas>
    </div>
</div>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
    <script>
        document.addEventListener('DOMContentLoaded', function() {
            renderAttendanceChart('attendanceOverviewChart', {
  presentCount: 90,
       absentCount: 5,
           lateCount: 5,
                totalAttendance: 100
        });
        });
    </script>
}
```

### Example 2: With Controller Data
```html
<!-- In your View -->
<canvas id="attendanceOverviewChart" 
        data-present="@Model.PresentCount"
        data-absent="@Model.AbsentCount"
        data-late="@Model.LateCount"
        data-total="@Model.TotalAttendance">
</canvas>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
}
```

### Example 3: Multiple Charts
```html
<canvas id="chart1"></canvas>
<canvas id="chart2"></canvas>

@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
    <script src="~/js/parent.js"></script>
    <script>
        document.addEventListener('DOMContentLoaded', function() {
        // Chart for Student 1
         renderAttendanceChart('chart1', {
 presentCount: 45, absentCount: 3, lateCount: 2, totalAttendance: 50
            });
         
            // Chart for Student 2
            renderAttendanceChart('chart2', {
              presentCount: 42, absentCount: 5, lateCount: 3, totalAttendance: 50
        });
        });
    </script>
}
```

## Function Parameters Quick Reference

```javascript
renderAttendanceChart(canvasId, data)
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| canvasId | string | Yes | ID of the canvas element |
| data.presentCount | number | Yes | Number of days present |
| data.absentCount | number | Yes | Number of days absent |
| data.lateCount | number | Yes | Number of late arrivals |
| data.totalAttendance | number | No | Total count (auto-calculated if missing) |

## What the Chart Looks Like

- **Type**: Doughnut chart with center label
- **Colors**: 
  - Green = Present
  - Red = Absent  
  - Yellow = Late
- **Center**: Shows Present percentage prominently
- **Legend**: At the bottom
- **Interactive**: Hover to see detailed percentages

## Need More Help?

Check the detailed documentation: `ATTENDANCE_CHART_USAGE.md`

Or look at the working example in: `Views/Parent/Dashboard.cshtml`

---

**That's it! You're ready to add attendance charts to your views!** ??
