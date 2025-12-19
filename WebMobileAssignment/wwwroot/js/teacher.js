// Teacher Portal JavaScript

// Initialize dark mode on page load (before DOMContentLoaded to prevent flash)
(function() {
    const savedTheme = localStorage.getItem('theme') || 'light';
    const htmlElement = document.documentElement;
    const darkModeIcon = document.getElementById('darkModeIcon');
    
    htmlElement.setAttribute('data-theme', savedTheme);
    
    // Update icon on initial load
    if (savedTheme === 'dark' && darkModeIcon) {
        darkModeIcon.classList.remove('bi-moon-fill');
        darkModeIcon.classList.add('bi-sun-fill');
    }
})();

document.addEventListener('DOMContentLoaded', function () {
    // ==================== DARK MODE TOGGLE ====================
    const darkModeToggle = document.getElementById('darkModeToggle');
    const darkModeIcon = document.getElementById('darkModeIcon');
    const htmlElement = document.documentElement;
    
    if (darkModeToggle) {
        darkModeToggle.addEventListener('click', function() {
            const currentTheme = htmlElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            
            // Update theme
            htmlElement.setAttribute('data-theme', newTheme);
            
            // Save to localStorage
            localStorage.setItem('theme', newTheme);
            
            // Update icon
            if (newTheme === 'dark') {
                darkModeIcon.classList.remove('bi-moon-fill');
                darkModeIcon.classList.add('bi-sun-fill');
            } else {
                darkModeIcon.classList.remove('bi-sun-fill');
                darkModeIcon.classList.add('bi-moon-fill');
            }
            
            console.log(`[Dark Mode] Switched to ${newTheme} mode`);
        });
    }

    // ==================== SIDEBAR TOGGLE ====================
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
