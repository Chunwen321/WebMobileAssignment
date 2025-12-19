// ==================== ADMIN PORTAL JAVASCRIPT ====================

// ==================== DARK MODE FUNCTIONALITY ====================
/**
 * Dark Mode Toggle with LocalStorage Persistence
 */
(function() {
    // Initialize dark mode from localStorage on page load
    const initDarkMode = () => {
        const savedTheme = localStorage.getItem('theme') || 'light';
        const htmlElement = document.documentElement;
        const darkModeIcon = document.getElementById('darkModeIcon');
   
        if (savedTheme === 'dark') {
            htmlElement.setAttribute('data-theme', 'dark');
            if (darkModeIcon) {
                darkModeIcon.classList.remove('bi-moon-fill');
                darkModeIcon.classList.add('bi-sun-fill');
            }
        }
    };
    
    // Call init immediately (before DOMContentLoaded) to prevent flash
    initDarkMode();
    
    // Setup toggle button after DOM loads
    document.addEventListener('DOMContentLoaded', function() {
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
    });
})();

// Wait for DOM to be fully loaded
document.addEventListener('DOMContentLoaded', function () {
    console.log('Admin portal JavaScript loaded');

    // ==================== SIDEBAR TOGGLE FOR MOBILE ====================
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.querySelector('.sidebar');
    const body = document.body;

    // Create overlay element for mobile sidebar
    let sidebarOverlay = document.querySelector('.sidebar-overlay');
    if (!sidebarOverlay) {
        sidebarOverlay = document.createElement('div');
        sidebarOverlay.className = 'sidebar-overlay';
        document.body.insertBefore(sidebarOverlay, document.body.firstChild);
    }

    // Toggle sidebar function
    function toggleSidebar() {
        sidebar.classList.toggle('show');
        sidebarOverlay.classList.toggle('show');
        body.classList.toggle('sidebar-open');

        // Store sidebar state in localStorage
        const isOpen = sidebar.classList.contains('show');
        localStorage.setItem('sidebarOpen', isOpen);
    }

    // Toggle sidebar on button click
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', toggleSidebar);
    }

    // Close sidebar when clicking on overlay
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function () {
            toggleSidebar();
        });
    }

    // Close sidebar on window resize if screen becomes large
    window.addEventListener('resize', function () {
        if (window.innerWidth > 992) {
            sidebar.classList.remove('show');
            sidebarOverlay.classList.remove('show');
            body.classList.remove('sidebar-open');
        }
    });

    // Close sidebar when clicking on navigation links (mobile only)
    if (window.innerWidth <= 992) {
        const sidebarLinks = document.querySelectorAll('.sidebar-item, .sidebar-subitem');
        sidebarLinks.forEach(link => {
            link.addEventListener('click', function (e) {
                // Don't close if it's a dropdown toggle
                if (!this.classList.contains('dropdown-toggle')) {
                    setTimeout(() => {
                        sidebar.classList.remove('show');
                        sidebarOverlay.classList.remove('show');
                        body.classList.remove('sidebar-open');
                    }, 300);
                }
            });
        });
    }

    // Restore sidebar state from localStorage on page load (desktop only)
    if (window.innerWidth > 992) {
        const sidebarWasOpen = localStorage.getItem('sidebarOpen') === 'true';
        if (sidebarWasOpen) {
            sidebar.classList.add('show');
        }
    }

    // ==================== DROPDOWN MENUS ====================
    const dropdownToggles = document.querySelectorAll('.sidebar-dropdown .dropdown-toggle');
    
    dropdownToggles.forEach(toggle => {
        toggle.addEventListener('click', function (e) {
            e.preventDefault();
            
            const parent = this.closest('.sidebar-dropdown');
            const collapse = parent.querySelector('.collapse');
            const icon = this.querySelector('i.bi-chevron-down');
            
            // Toggle the dropdown
            if (collapse.classList.contains('show')) {
                collapse.classList.remove('show');
                this.setAttribute('aria-expanded', 'false');
            } else {
                collapse.classList.add('show');
                this.setAttribute('aria-expanded', 'true');
            }
        });
    });

    // ==================== RESPONSIVE TABLES ====================
    // Add horizontal scroll indicator for tables on mobile
    const tables = document.querySelectorAll('.table-responsive');
    
    tables.forEach(tableContainer => {
        const table = tableContainer.querySelector('table');
        if (table) {
            // Add scroll hint if table is wider than container
            function checkTableScroll() {
                if (table.scrollWidth > tableContainer.clientWidth) {
                    tableContainer.classList.add('has-scroll');
                } else {
                    tableContainer.classList.remove('has-scroll');
                }
            }

            checkTableScroll();
            window.addEventListener('resize', checkTableScroll);
        }
    });

    // ==================== FORM VALIDATION HELPERS ====================
    // Add touch-friendly form validation feedback
    const forms = document.querySelectorAll('.needs-validation');
    
    forms.forEach(form => {
        form.addEventListener('submit', function (event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
                
                // Scroll to first invalid field on mobile
                const firstInvalid = form.querySelector(':invalid');
                if (firstInvalid && window.innerWidth <= 768) {
                    firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    firstInvalid.focus();
                }
            }
            
            form.classList.add('was-validated');
        }, false);
    });

    // ==================== MOBILE-FRIENDLY ALERTS ====================
    // Auto-dismiss alerts after 5 seconds on mobile
    if (window.innerWidth <= 768) {
        const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
        alerts.forEach(alert => {
            setTimeout(() => {
                alert.style.transition = 'opacity 0.3s ease';
                alert.style.opacity = '0';
                setTimeout(() => {
                    alert.remove();
                }, 300);
            }, 5000);
        });
    }

    // ==================== TOUCH GESTURES FOR MOBILE ====================
    // Add swipe gesture to close sidebar on mobile
    let touchStartX = 0;
    let touchEndX = 0;

    if (sidebar && window.innerWidth <= 992) {
        sidebar.addEventListener('touchstart', function (e) {
            touchStartX = e.changedTouches[0].screenX;
        }, { passive: true });

        sidebar.addEventListener('touchend', function (e) {
            touchEndX = e.changedTouches[0].screenX;
            handleSwipe();
        }, { passive: true });

        function handleSwipe() {
            // Swipe left to close sidebar (minimum 50px swipe)
            if (touchEndX < touchStartX - 50 && sidebar.classList.contains('show')) {
                toggleSidebar();
            }
        }
    }

    // ==================== CONFIRM DIALOGS FOR DESTRUCTIVE ACTIONS ====================
    const deleteButtons = document.querySelectorAll('[data-confirm]');
    
    deleteButtons.forEach(button => {
        button.addEventListener('click', function (e) {
            const message = this.getAttribute('data-confirm') || 'Are you sure?';
            if (!confirm(message)) {
                e.preventDefault();
                return false;
            }
        });
    });

    // ==================== MOBILE TABLE ENHANCEMENTS ====================
    // Convert tables to mobile-friendly cards on small screens
    function enhanceTablesForMobile() {
        if (window.innerWidth <= 768) {
            const tables = document.querySelectorAll('.table:not(.table-mobile-enhanced)');
            
            tables.forEach(table => {
                const headerCells = table.querySelectorAll('thead th');
                const bodyRows = table.querySelectorAll('tbody tr');
                
                bodyRows.forEach(row => {
                    const cells = row.querySelectorAll('td');
                    cells.forEach((cell, index) => {
                        if (headerCells[index]) {
                            const label = headerCells[index].textContent.trim();
                            cell.setAttribute('data-label', label);
                        }
                    });
                });
                
                table.classList.add('table-mobile-enhanced');
            });
        }
    }

    enhanceTablesForMobile();
    window.addEventListener('resize', enhanceTablesForMobile);

    // ==================== LOADING INDICATORS ====================
    // Add loading state to forms on submit
    const submitButtons = document.querySelectorAll('form button[type="submit"]');
    
    submitButtons.forEach(button => {
        const form = button.closest('form');
        if (form) {
            form.addEventListener('submit', function () {
                if (form.checkValidity()) {
                    button.disabled = true;
                    const originalText = button.innerHTML;
                    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
                    
                    // Re-enable after 5 seconds (failsafe)
                    setTimeout(() => {
                        button.disabled = false;
                        button.innerHTML = originalText;
                    }, 5000);
                }
            });
        }
    });

    // ==================== SMOOTH SCROLL FOR ANCHORS ====================
    const anchorLinks = document.querySelectorAll('a[href^="#"]');
    
    anchorLinks.forEach(link => {
        link.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId !== '#' && targetId !== '#!') {
                const target = document.querySelector(targetId);
                if (target) {
                    e.preventDefault();
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            }
        });
    });

    // ==================== IMAGE PREVIEW FOR FILE UPLOADS ====================
    const fileInputs = document.querySelectorAll('input[type="file"][accept*="image"]');
    
    fileInputs.forEach(input => {
        input.addEventListener('change', function (e) {
            const file = e.target.files[0];
            if (file && file.type.startsWith('image/')) {
                const reader = new FileReader();
                reader.onload = function (event) {
                    // Find the preview image element (if exists)
                    const previewId = input.getAttribute('data-preview-target');
                    if (previewId) {
                        const preview = document.getElementById(previewId);
                        if (preview) {
                            preview.src = event.target.result;
                        }
                    }
                };
                reader.readAsDataURL(file);
            }
        });
    });

    // ==================== COPY TO CLIPBOARD FUNCTIONALITY ====================
    const copyButtons = document.querySelectorAll('[data-copy]');
    
    copyButtons.forEach(button => {
        button.addEventListener('click', function () {
            const textToCopy = this.getAttribute('data-copy');
            navigator.clipboard.writeText(textToCopy).then(() => {
                // Show feedback
                const originalText = this.innerHTML;
                this.innerHTML = '<i class="bi bi-check"></i> Copied!';
                setTimeout(() => {
                    this.innerHTML = originalText;
                }, 2000);
            }).catch(err => {
                console.error('Failed to copy:', err);
            });
        });
    });

    // ==================== VIEWPORT HEIGHT FIX FOR MOBILE ====================
    // Fix viewport height issue on mobile browsers
    function setVH() {
        let vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    }

    setVH();
    window.addEventListener('resize', setVH);
    window.addEventListener('orientationchange', setVH);

    // ==================== PERFORMANCE OPTIMIZATION ====================
    // Lazy load images on mobile
    if ('IntersectionObserver' in window && window.innerWidth <= 768) {
        const lazyImages = document.querySelectorAll('img[data-src]');
        
        const imageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    img.src = img.getAttribute('data-src');
                    img.removeAttribute('data-src');
                    imageObserver.unobserve(img);
                }
            });
        });

        lazyImages.forEach(img => imageObserver.observe(img));
    }

    console.log('Admin portal JavaScript initialization complete');
});

// ==================== HELPER FUNCTIONS ====================

// Show toast notification (if Bootstrap 5 is available)
function showToast(message, type = 'info') {
    if (typeof bootstrap !== 'undefined') {
        const toastHTML = `
            <div class="toast align-items-center text-white bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">${message}</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;
        
        let toastContainer = document.querySelector('.toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }
        
        toastContainer.insertAdjacentHTML('beforeend', toastHTML);
        const toastElement = toastContainer.lastElementChild;
        const toast = new bootstrap.Toast(toastElement);
        toast.show();
        
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    } else {
        // Fallback to alert
        alert(message);
    }
}

// Confirm dialog with custom message
function confirmAction(message) {
    return confirm(message || 'Are you sure you want to proceed?');
}

// Format number with commas
function formatNumber(num) {
    return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

// Debounce function for search inputs
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Export functions for use in other scripts
window.AdminPortal = {
    showToast,
    confirmAction,
    formatNumber,
    debounce
};
