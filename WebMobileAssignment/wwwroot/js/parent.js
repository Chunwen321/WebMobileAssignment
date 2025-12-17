// Parent Portal JavaScript

document.addEventListener('DOMContentLoaded', function () {
    // Sidebar Toggle for Mobile
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.querySelector('.sidebar');
    
    if (sidebarToggle) {
   sidebarToggle.addEventListener('click', function () {
   sidebar.classList.toggle('show');
   
    // Create overlay if it doesn't exist
       let overlay = document.querySelector('.sidebar-overlay');
       if (!overlay) {
  overlay = document.createElement('div');
         overlay.className = 'sidebar-overlay';
     document.body.appendChild(overlay);
 
          // Close sidebar when clicking overlay
       overlay.addEventListener('click', function () {
         sidebar.classList.remove('show');
    overlay.classList.remove('show');
        });
            }
   overlay.classList.toggle('show');
  });
    }

    // Close sidebar when clicking on a link (mobile)
    if (window.innerWidth <= 992) {
   const sidebarLinks = document.querySelectorAll('.sidebar-item, .sidebar-subitem');
        sidebarLinks.forEach(link => {
  link.addEventListener('click', function () {
   if (!this.classList.contains('dropdown-toggle')) {
      sidebar.classList.remove('show');
  const overlay = document.querySelector('.sidebar-overlay');
     if (overlay) {
  overlay.classList.remove('show');
          }
  }
            });
        });
    }
});

/**
 * Initialize event listeners for enrolled classes pagination
 * This function should be called after the content is loaded (both initially and via AJAX)
 */
function initializeEnrolledClassesPagination() {
    function attachPaginationListeners() {
        const classNavButtons = document.querySelectorAll('.class-nav-btn');
  
        // Remove existing listeners to prevent duplicates
        classNavButtons.forEach(button => {
            // Clone and replace to remove all event listeners
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
       
    // Try to get studentId from the page context if not found
             const urlParams = new URLSearchParams(window.location.search);
const finalStudentId = studentId || urlParams.get('studentId');
        
     if (!page || page < 1 || !finalStudentId) return;
       
     // Show loading state
    const allButtons = document.querySelectorAll('.class-nav-btn');
      allButtons.forEach(btn => btn.disabled = true);
       
  const originalHtml = this.innerHTML;
     this.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>';
                
  // Fetch paginated classes via AJAX
  fetch(`/Parent/GetEnrolledClasses?studentId=${finalStudentId}&page=${page}`)
              .then(response => response.json())
           .then(data => {
              if (data.success) {
     updateEnrolledClasses(data);
        } else {
          alert('Error: ' + data.message);
 this.innerHTML = originalHtml;
             allButtons.forEach(btn => btn.disabled = false);
          }
                    })
     .catch(error => {
        console.error('Error:', error);
 alert('An error occurred while loading classes');
  this.innerHTML = originalHtml;
      allButtons.forEach(btn => btn.disabled = false);
        });
     });
 });
    }
    
    function updateEnrolledClasses(data) {
        const container = document.getElementById('enrolledClassesContainer');
        if (!container) return;
        
   // Build the complete HTML including classes and pagination
        let html = '<div class="row g-3">';
        
        data.enrollments.forEach(enrollment => {
      let scheduleHtml = '';
   if (enrollment.day) {
       scheduleHtml = `
           <div class="mt-2">
             <small class="text-muted">Schedule:</small>
          <span class="badge bg-info ms-1">${enrollment.day}</span>
${enrollment.startTime && enrollment.endTime ? `<span class="ms-1">${enrollment.startTime} - ${enrollment.endTime}</span>` : ''}
         </div>
    `;
    }
            
        let teacherHtml = '';
            if (enrollment.teacherName) {
       teacherHtml = `
            <div class="mt-2">
             <small class="text-muted">Teacher:</small>
 <strong class="ms-1">${enrollment.teacherName}</strong>
             </div>
       `;
            }
         
     html += `
         <div class="col-md-6">
      <div class="p-3" style="background: #f8f9fa; border-radius: 8px;">
             <div class="d-flex align-items-center gap-3">
     <div class="card-icon blue">
    <i class="bi bi-mortarboard-fill"></i>
         </div>
        <div>
          <small class="text-muted d-block">Class Name</small>
      <strong>${enrollment.className}</strong>
                   </div>
             </div>
     ${scheduleHtml}
        ${teacherHtml}
 </div>
    </div>
         `;
   });
  
        html += '</div>';
        
        // Add pagination if more than 1 page
        if (data.totalPages > 1) {
 html += `
       <div class="d-flex justify-content-between align-items-center mt-3 px-3">
   <button type="button" class="btn btn-sm btn-outline-primary class-nav-btn" 
          data-page="${data.currentPage - 1}" 
           data-action="previous"
 ${data.currentPage <= 1 ? 'disabled' : ''}>
       <i class="bi bi-chevron-left"></i> Previous
          </button>
    
  <span class="text-muted">
       Page <strong class="currentClassPage">${data.currentPage}</strong> of <strong class="totalClassPages">${data.totalPages}</strong>
       </span>
    
     <button type="button" class="btn btn-sm btn-outline-primary class-nav-btn" 
   data-page="${data.currentPage + 1}" 
      data-action="next"
    ${data.currentPage >= data.totalPages ? 'disabled' : ''}>
    Next <i class="bi bi-chevron-right"></i>
          </button>
        </div>
            `;
        }

        // Update container
        container.innerHTML = html;
        
        // Re-attach event listeners to new buttons
        attachPaginationListeners();
    }
    
    // Initial setup
    attachPaginationListeners();
}

/**
 * Renders an attendance visualization chart using Chart.js
 * @param {string} canvasId - The ID of the canvas element where the chart will be rendered
 * @param {Object} data - The attendance data object
 * @param {number} data.presentCount - Number of present days
 * @param {number} data.absentCount - Number of absent days
 * @param {number} data.lateCount - Number of late arrivals
 * @param {number} data.totalAttendance - Total attendance records
 * @returns {Chart|null} - The Chart.js instance or null if Chart.js is not loaded
 */
function renderAttendanceChart(canvasId, data) {
    // Check if Chart.js is loaded
  if (typeof Chart === 'undefined') {
   console.error('Chart.js is not loaded. Please include Chart.js library.');
  return null;
    }

    // Get canvas element
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
      console.error(`Canvas element with ID "${canvasId}" not found.`);
    return null;
    }

    // Validate data
    const presentCount = data.presentCount || 0;
    const absentCount = data.absentCount || 0;
    const lateCount = data.lateCount || 0;
    const totalAttendance = data.totalAttendance || (presentCount + absentCount + lateCount);

    // Calculate percentages
    let dataPercents = [0, 0, 0];
 if (totalAttendance > 0) {
        dataPercents = [
         Math.round((presentCount / totalAttendance) * 100),
            Math.round((absentCount / totalAttendance) * 100),
  Math.round((lateCount / totalAttendance) * 100)
  ];
    }

    // Destroy existing chart if it exists
    const existingChart = Chart.getChart(canvasId);
    if (existingChart) {
        existingChart.destroy();
    }

    // Create the chart
    const ctx = canvas.getContext('2d');
 const chart = new Chart(ctx, {
      type: 'doughnut',
  data: {
  labels: ['Present', 'Absent', 'Late'],
        datasets: [{
data: dataPercents,
     backgroundColor: [
  'rgba(25, 135, 84, 0.95)',// Present (green)
        'rgba(220, 53, 69, 0.95)',   // Absent (red)
       'rgba(255, 193, 7, 0.95)'    // Late (amber)
     ],
  borderWidth: 0,
   hoverOffset: 6
   }]
    },
     options: {
      responsive: true,
            maintainAspectRatio: false,
        cutout: '65%', // donut thickness
            plugins: {
       legend: {
       position: 'bottom',
         labels: {
  usePointStyle: true,
           padding: 16,
        font: {
         size: 12,
      family: "'Segoe UI', 'Roboto', sans-serif"
 }
   }
    },
        tooltip: {
  callbacks: {
  label: function (context) {
   const label = context.label || '';
      const value = context.parsed || 0;
      return label + ': ' + value + '%';
  }
  }
         }
  }
     }
    });

    // Add center label overlay
    addChartCenterLabel(canvas, dataPercents[0], 'Present');

    return chart;
}

/**
 * Adds a center label to a doughnut chart
 * @param {HTMLCanvasElement} canvas - The canvas element containing the chart
 * @param {number} percentage - The percentage to display
 * @param {string} label - The label text to display
 */
function addChartCenterLabel(canvas, percentage, label) {
    const container = canvas.parentElement;
    
    // Remove existing center label if any
    const existingLabel = container.querySelector('.chart-center');
    if (existingLabel) {
        existingLabel.remove();
    }

  // Create new center label
    const div = document.createElement('div');
    div.className = 'chart-center';
div.style.position = 'absolute';
    div.style.left = '50%';
 div.style.top = '50%';
    div.style.transform = 'translate(-50%, -50%)';
    div.style.textAlign = 'center';
  div.style.pointerEvents = 'none';

    div.innerHTML = `
 <div style="font-weight: 700; font-size: 1.4rem; color: #198754;">
  ${percentage}%
      </div>
        <div style="font-size: 0.9rem; color: #6c757d;">
         ${label}
        </div>
    `;

    // Make container relative positioned
    container.style.position = 'relative';
    container.appendChild(div);
}

// NOTE: Auto-initialization is disabled to prevent conflicts with page-specific initialization
// Pages should call renderAttendanceChart() manually after DOM is ready
// Example usage in your view's script section:
//   document.addEventListener('DOMContentLoaded', function() {
// renderAttendanceChart('attendanceOverviewChart', {
//           presentCount: 45,
//           absentCount: 3,
//           lateCount: 2,
//           totalAttendance: 50
//    });
//   });

// Export function for use in other scripts
window.renderAttendanceChart = renderAttendanceChart;
window.addChartCenterLabel = addChartCenterLabel;
