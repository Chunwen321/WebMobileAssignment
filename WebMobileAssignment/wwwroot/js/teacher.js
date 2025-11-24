// Teacher Portal JavaScript

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
