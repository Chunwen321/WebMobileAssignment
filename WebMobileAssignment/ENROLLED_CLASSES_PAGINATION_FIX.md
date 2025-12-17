# Enrolled Classes Pagination Fix

## Problem
When navigating between children in the Student Profile page, the enrolled classes pagination buttons stopped working after returning to a previously viewed child. The "Next" button would not respond to clicks.

## Root Cause
The pagination event listeners were defined in an inline `<script>` tag inside the `_StudentProfileContent.cshtml` partial view. When the child profile was loaded via AJAX, the HTML content was injected using `innerHTML`, but **inline scripts are not executed** when content is inserted this way. This meant:

1. **First visit**: The script executes normally, event listeners are attached, pagination works
2. **Navigate away**: Content is replaced via AJAX, old event listeners are lost
3. **Navigate back**: New content is injected, but the inline script doesn't run, so no new event listeners are attached
4. **Result**: Pagination buttons don't work

## Solution
Moved the pagination initialization logic from the inline script in the partial view to a reusable global function in `parent.js`:

### Changes Made

#### 1. `parent.js` - Added Global Initialization Function
```javascript
/**
 * Initialize event listeners for enrolled classes pagination
 * This function should be called after the content is loaded (both initially and via AJAX)
 */
function initializeEnrolledClassesPagination() {
    // Function to attach pagination listeners
    function attachPaginationListeners() {
        const classNavButtons = document.querySelectorAll('.class-nav-btn');
        
        // Remove existing listeners to prevent duplicates
      classNavButtons.forEach(button => {
            const newButton = button.cloneNode(true);
            button.parentNode.replaceChild(newButton, button);
        });
   
        // Attach new listeners
        const refreshedButtons = document.querySelectorAll('.class-nav-btn');
        refreshedButtons.forEach(button => {
     button.addEventListener('click', function(e) {
    e.preventDefault();
    e.stopPropagation();
         
      const page = parseInt(this.getAttribute('data-page'));
const studentId = this.closest('.dashboard-card')?.querySelector('[data-student-id]')?.getAttribute('data-student-id');
   
     // Try to get studentId from URL if not found
         const urlParams = new URLSearchParams(window.location.search);
      const finalStudentId = studentId || urlParams.get('studentId');
         
       if (!page || page < 1 || !finalStudentId) return;
  
    // Show loading state and fetch paginated classes via AJAX
    // ... (AJAX fetch logic)
            });
        });
    }
    
    function updateEnrolledClasses(data) {
        // Updates the enrolled classes container with new content
        // and re-attaches pagination listeners
    }
    
    // Initial setup
    attachPaginationListeners();
}
```

#### 2. `_StudentProfileContent.cshtml` - Simplified Script
Changed from inline event handler setup to simple function call:
```html
<div class="dashboard-card mb-3" data-student-id="@student?.StudentId">
    <!-- Class content here -->
</div>

<script>
// Initialize pagination after content is loaded
(function() {
    if (typeof initializeEnrolledClassesPagination === 'function') {
      initializeEnrolledClassesPagination();
    }
})();
</script>
```

#### 3. `StudentProfile.cshtml` - Re-initialize After AJAX
Added call to reinitialize pagination after AJAX content is loaded:
```javascript
function loadStudentProfile(studentId) {
    // ... AJAX fetch code ...
    .then(data => {
        if (data.success) {
         // Update profile content
   profileContent.innerHTML = data.html;
            
        // Re-initialize pagination after content is loaded
        if (typeof initializeEnrolledClassesPagination === 'function') {
                initializeEnrolledClassesPagination();
          }
    }
    });
}

// Also initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    if (typeof initializeEnrolledClassesPagination === 'function') {
  initializeEnrolledClassesPagination();
    }
});
```

## Key Improvements

1. **Reusable Function**: The pagination logic is now in a global function that can be called multiple times
2. **Prevents Duplicates**: Event listeners are properly cleaned up before being re-attached
3. **Works with AJAX**: The function is called both on initial page load and after AJAX content updates
4. **Student ID Handling**: Improved logic to find the student ID from either the DOM or URL parameters
5. **Better Separation**: JavaScript logic is separated from the view template

## Testing
To verify the fix works:
1. Navigate to Student Profile page
2. Click "Next" on enrolled classes pagination - should work ?
3. Click "Next" on child navigation to go to another child
4. Click "Previous" on child navigation to return to first child
5. Click "Next" on enrolled classes pagination - should now work ?

## Files Modified
- `WebMobileAssignment\wwwroot\js\parent.js`
- `WebMobileAssignment\Views\Parent\_StudentProfileContent.cshtml`
- `WebMobileAssignment\Views\Parent\StudentProfile.cshtml`
