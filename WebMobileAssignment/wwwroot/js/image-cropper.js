/**
 * Image Cropper Module
 * Reusable image cropping functionality with webcam support
 * Requires: Cropper.js, Bootstrap 5
 */

const ImageCropper = (function() {
    'use strict';

    // Private variables
    let cropper = null;
    let currentTarget = null;
    let scaleX = 1;
    let scaleY = 1;
    let currentRotation = 0;
    
    // Configuration object to store target-specific settings
    const targets = {};

    /**
     * Initialize image cropper for a specific target
     * @param {Object} config - Configuration object
     * @param {string} config.targetId - Unique identifier for this target (e.g., 'student', 'parent')
     * @param {string} config.previewElementId - ID of the preview element
     * @param {string} config.fileInputId - ID of the file input element
     * @param {string} config.clearButtonId - ID of the clear button
     * @param {string} config.editButtonId - ID of the edit button (optional)
     * @param {number} config.previewWidth - Width of the preview circle (default: 200)
     * @param {number} config.previewHeight - Height of the preview circle (default: 200)
     */
    function init(config) {
        if (!config.targetId || !config.previewElementId || !config.fileInputId) {
            console.error('ImageCropper: Missing required configuration');
            return;
        }

        // Store configuration
        targets[config.targetId] = {
            previewElementId: config.previewElementId,
            fileInputId: config.fileInputId,
            clearButtonId: config.clearButtonId,
            editButtonId: config.editButtonId,
            previewWidth: config.previewWidth || 200,
            previewHeight: config.previewHeight || 200
        };

        // Setup file input change handler
        const fileInput = document.getElementById(config.fileInputId);
        if (fileInput) {
            fileInput.addEventListener('change', function(event) {
                handleFileSelect(event, config.targetId);
            });
        }

        // Setup edit button handler if provided
        if (config.editButtonId) {
            setupEditButton(config.targetId);
        }

        // Setup clear button handler if provided
        if (config.clearButtonId) {
            setupClearButton(config.targetId);
        }
    }

    /**
     * Handle file selection
     */
    function handleFileSelect(event, targetId) {
        const file = event.target.files[0];
        
        if (!file) return;

        // Validate file size (5MB)
        if (file.size > 5 * 1024 * 1024) {
            alert('File size must not exceed 5MB');
            event.target.value = '';
            return;
        }

        // Validate file type
        const validTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
        if (!validTypes.includes(file.type.toLowerCase())) {
            alert('Invalid file type. Please upload JPG, PNG, GIF, or WEBP image.');
            event.target.value = '';
            return;
        }

        // Read file and open cropper
        const reader = new FileReader();
        reader.onload = function(e) {
            openCropper(e.target.result, targetId);
        };
        reader.readAsDataURL(file);
    }

    /**
     * Open cropper modal with image
     */
    function openCropper(imageSrc, targetId) {
        currentTarget = targetId;
        const image = document.getElementById('cropperImage');
        const modalElement = document.getElementById('imageCropperModal');

        if (!image || !modalElement) {
            console.error('ImageCropper: Modal elements not found');
            return;
        }

        // Set image source
        image.src = imageSrc;

        // Destroy existing cropper if any
        if (cropper) {
            cropper.destroy();
            cropper = null;
        }

        // Create and show modal
        const modal = new bootstrap.Modal(modalElement);
        modal.show();

        // Initialize cropper after modal is shown
        modalElement.addEventListener('shown.bs.modal', function initCropperAfterShow() {
            modalElement.removeEventListener('shown.bs.modal', initCropperAfterShow);

            setTimeout(function() {
                cropper = new Cropper(image, {
                    aspectRatio: NaN,
                    viewMode: 1,
                    dragMode: 'move',
                    autoCropArea: 0.8,
                    restore: false,
                    guides: true,
                    center: true,
                    highlight: true,
                    cropBoxMovable: true,
                    cropBoxResizable: true,
                    toggleDragModeOnDblclick: false,
                    ready: function() {
                        scaleX = 1;
                        scaleY = 1;
                        currentRotation = 0;
                        document.getElementById('rotateSlider').value = 0;
                        document.getElementById('rotateValue').textContent = '0°';
                        updatePreview();
                    },
                    crop: function() {
                        updatePreview();
                    },
                    zoom: function() {
                        setTimeout(updatePreview, 50);
                    },
                    rotate: function() {
                        setTimeout(updatePreview, 50);
                    }
                });
            }, 150);
        });
    }

    /**
     * Update preview circle
     */
    function updatePreview() {
        if (!cropper) return;

        const canvas = cropper.getCroppedCanvas({
            width: 150,
            height: 150,
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high',
            fillColor: '#fff'
        });

        if (canvas) {
            const previewElement = document.getElementById('cropperPreview');
            previewElement.innerHTML = '';

            const img = document.createElement('img');
            img.src = canvas.toDataURL('image/jpeg', 0.9);
            img.style.width = '100%';
            img.style.height = '100%';
            img.style.objectFit = 'cover';
            img.style.display = 'block';

            previewElement.appendChild(img);
        }
    }

    /**
     * Setup cropper controls
     */
    function setupControls() {
        // Zoom controls
        document.getElementById('zoomIn')?.addEventListener('click', () => cropper?.zoom(0.1));
        document.getElementById('zoomOut')?.addEventListener('click', () => cropper?.zoom(-0.1));
        document.getElementById('resetZoom')?.addEventListener('click', function() {
            cropper?.reset();
            scaleX = 1;
            scaleY = 1;
            currentRotation = 0;
            document.getElementById('rotateSlider').value = 0;
            document.getElementById('rotateValue').textContent = '0°';
        });

        // Rotation controls
        document.getElementById('rotateSlider')?.addEventListener('input', function(e) {
            const angle = parseInt(e.target.value);
            currentRotation = angle;
            cropper?.rotateTo(angle);
            document.getElementById('rotateValue').textContent = angle + '°';
        });

        document.getElementById('rotateLeft90')?.addEventListener('click', function() {
            currentRotation -= 90;
            if (currentRotation < -180) currentRotation += 360;
            cropper?.rotateTo(currentRotation);
            document.getElementById('rotateSlider').value = currentRotation;
            document.getElementById('rotateValue').textContent = currentRotation + '°';
        });

        document.getElementById('rotateRight90')?.addEventListener('click', function() {
            currentRotation += 90;
            if (currentRotation > 180) currentRotation -= 360;
            cropper?.rotateTo(currentRotation);
            document.getElementById('rotateSlider').value = currentRotation;
            document.getElementById('rotateValue').textContent = currentRotation + '°';
        });

        // Flip controls
        document.getElementById('flipHorizontal')?.addEventListener('click', function() {
            scaleX = scaleX === 1 ? -1 : 1;
            cropper?.scaleX(scaleX);
        });

        document.getElementById('flipVertical')?.addEventListener('click', function() {
            scaleY = scaleY === 1 ? -1 : 1;
            cropper?.scaleY(scaleY);
        });

        // Reset all
        document.getElementById('resetAll')?.addEventListener('click', function() {
            cropper?.reset();
            scaleX = 1;
            scaleY = 1;
            currentRotation = 0;
            document.getElementById('rotateSlider').value = 0;
            document.getElementById('rotateValue').textContent = '0°';
        });

        // Crop and save
        document.getElementById('cropAndSave')?.addEventListener('click', function() {
            saveCroppedImage();
        });
    }

    /**
     * Save cropped image
     */
    function saveCroppedImage() {
        if (!cropper || !currentTarget) return;

        const canvas = cropper.getCroppedCanvas({
            width: 400,
            height: 400,
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high',
        });

        canvas.toBlob(function(blob) {
            const timestamp = Date.now();
            const file = new File([blob], `cropped-photo-${timestamp}.jpg`, {
                type: 'image/jpeg',
                lastModified: timestamp
            });

            updatePhoto(currentTarget, file, canvas.toDataURL('image/jpeg', 0.9));

            // Close modal
            const modalElement = document.getElementById('imageCropperModal');
            const modal = bootstrap.Modal.getInstance(modalElement);
            modal?.hide();

            // Destroy cropper
            if (cropper) {
                cropper.destroy();
                cropper = null;
            }
        }, 'image/jpeg', 0.9);
    }

    /**
     * Update photo preview and file input
     */
    function updatePhoto(targetId, file, dataUrl) {
        const config = targets[targetId];
        if (!config) return;

        const fileInput = document.getElementById(config.fileInputId);
        const preview = document.getElementById(config.previewElementId);
        const clearBtn = config.clearButtonId ? document.getElementById(config.clearButtonId) : null;
        const editBtn = config.editButtonId ? document.getElementById(config.editButtonId) : null;

        // Update file input
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        fileInput.files = dataTransfer.files;

        // Update preview
        preview.outerHTML = `<img id="${config.previewElementId}" 
                                 src="${dataUrl}" 
                                 alt="Profile Picture" 
                                 class="img-thumbnail rounded-circle mb-3" 
                                 style="width: ${config.previewWidth}px; height: ${config.previewHeight}px; object-fit: cover; cursor: pointer;"
                                 onclick="ImageCropper.enlargeImage('${dataUrl}')"
                                 title="Click to enlarge">`;

        // Show buttons
        if (clearBtn) clearBtn.style.display = 'block';
        if (editBtn) editBtn.style.display = 'block';
    }

    /**
     * Setup edit button
     */
    function setupEditButton(targetId) {
        document.addEventListener('click', function(e) {
            const config = targets[targetId];
            if (!config || !config.editButtonId) return;

            if (e.target && e.target.id === config.editButtonId) {
                const preview = document.getElementById(config.previewElementId);
                if (preview && preview.tagName === 'IMG') {
                    openCropper(preview.src, targetId);
                }
            }
        });
    }

    /**
     * Setup clear button
     */
    function setupClearButton(targetId) {
        const config = targets[targetId];
        if (!config) return;

        const clearBtn = document.getElementById(config.clearButtonId);
        if (!clearBtn) return;

        clearBtn.addEventListener('click', function() {
            clearPhoto(targetId);
        });
    }

    /**
     * Clear photo
     */
    function clearPhoto(targetId) {
        const config = targets[targetId];
        if (!config) return;

        const fileInput = document.getElementById(config.fileInputId);
        const preview = document.getElementById(config.previewElementId);
        const clearBtn = config.clearButtonId ? document.getElementById(config.clearButtonId) : null;
        const editBtn = config.editButtonId ? document.getElementById(config.editButtonId) : null;

        fileInput.value = '';

        // Calculate font size based on preview height (half of height)
        const iconSize = (config.previewHeight / 2) / 10;

        preview.outerHTML = `<div id="${config.previewElementId}" 
                                 class="d-inline-flex align-items-center justify-content-center rounded-circle mb-3 bg-light" 
                                 style="width: ${config.previewWidth}px; height: ${config.previewHeight}px;">
                               <i class="bi bi-person-circle text-secondary" style="font-size: ${iconSize}rem;"></i>
                             </div>`;

        if (clearBtn) clearBtn.style.display = 'none';
        if (editBtn) editBtn.style.display = 'none';
    }

    /**
     * Enlarge image in modal
     */
    function enlargeImage(imageSrc) {
        const enlargedImg = document.getElementById('enlargedImage');
        const modalElement = document.getElementById('imageEnlargeModal');
        if (enlargedImg && modalElement) {
            enlargedImg.src = imageSrc;
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    }

    /**
     * Get cropper modal HTML
     */
    function getModalHTML() {
        return `
    <!-- Image Enlargement Modal -->
    <div class="modal fade" id="imageEnlargeModal" tabindex="-1" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered modal-xl">
            <div class="modal-content bg-dark">
                <div class="modal-header border-0">
                    <h5 class="modal-title text-white">Profile Picture</h5>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                </div>
                <div class="modal-body text-center p-4">
                    <img id="enlargedImage" src="" alt="Enlarged Profile Picture" class="img-fluid" style="max-height: 80vh; border-radius: 10px;">
                </div>
            </div>
        </div>
    </div>

    <!-- Image Cropper Modal -->
    <div class="modal fade" id="imageCropperModal" tabindex="-1" aria-labelledby="imageCropperModalLabel" aria-hidden="true" data-bs-backdrop="static">
        <div class="modal-dialog modal-xl modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header bg-success text-white">
                    <h5 class="modal-title" id="imageCropperModalLabel">
                        <i class="bi bi-crop me-2"></i>Edit Photo - Crop, Rotate & Adjust
                    </h5>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                </div>
                <div class="modal-body p-4">
                    <div class="row">
                        <div class="col-md-8">
                            <div class="img-container bg-dark rounded" style="height: 600px; display: flex; align-items: center; justify-content: center; overflow: hidden;">
                                <img id="cropperImage" src="" alt="Image for cropping" style="max-width: 100%; max-height: 100%; display: block;">
                            </div>
                        </div>
                        <div class="col-md-4">
                            <div class="tools-panel" style="position: sticky; top: 20px;">
                                <h6 class="mb-3 fw-bold text-success">
                                    <i class="bi bi-sliders me-2"></i>Tools
                                </h6>
                                
                                <!-- Zoom Controls -->
                                <div class="mb-4">
                                    <label class="form-label fw-bold small text-muted mb-2">
                                        <i class="bi bi-zoom-in me-1"></i>Zoom
                                    </label>
                                    <div class="d-flex gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="zoomIn" title="Zoom In">
                                            <i class="bi bi-plus-lg"></i>
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="zoomOut" title="Zoom Out">
                                            <i class="bi bi-dash-lg"></i>
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-secondary flex-fill" id="resetZoom" title="Reset">
                                            <i class="bi bi-arrow-counterclockwise"></i>
                                        </button>
                                    </div>
                                </div>

                                <!-- Rotate Controls with Slider -->
                                <div class="mb-4">
                                    <label class="form-label fw-bold small text-muted mb-2">
                                        <i class="bi bi-arrow-clockwise me-1"></i>Rotate
                                    </label>
                                    <div class="d-flex align-items-center gap-2 mb-2">
                                        <input type="range" class="form-range flex-grow-1" id="rotateSlider" min="-180" max="180" value="0" step="1">
                                        <span class="badge bg-success" id="rotateValue" style="min-width: 55px;">0&deg;</span>
                                    </div>
                                    <div class="d-flex gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="rotateLeft90" title="Rotate Left 90°">
                                            <i class="bi bi-arrow-counterclockwise"></i> 90&deg;
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="rotateRight90" title="Rotate Right 90°">
                                            <i class="bi bi-arrow-clockwise"></i> 90&deg;
                                        </button>
                                    </div>
                                </div>

                                <!-- Flip Controls -->
                                <div class="mb-4">
                                    <label class="form-label fw-bold small text-muted mb-2">
                                        <i class="bi bi-symmetry-horizontal me-1"></i>Flip
                                    </label>
                                    <div class="d-flex gap-2">
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="flipHorizontal" title="Flip Horizontal">
                                            <i class="bi bi-symmetry-vertical"></i> H
                                        </button>
                                        <button type="button" class="btn btn-sm btn-outline-success flex-fill" id="flipVertical" title="Flip Vertical">
                                            <i class="bi bi-symmetry-horizontal"></i> V
                                        </button>
                                    </div>
                                </div>

                                <!-- Reset All -->
                                <div class="mb-4">
                                    <button type="button" class="btn btn-sm btn-outline-danger w-100" id="resetAll">
                                        <i class="bi bi-arrow-counterclockwise me-1"></i>Reset All
                                    </button>
                                </div>

                                <hr class="my-3">

                                <!-- Preview -->
                                <div class="mt-3">
                                    <label class="form-label fw-bold small text-muted mb-2">
                                        <i class="bi bi-eye me-1"></i>Preview
                                    </label>
                                    <div class="preview-container border rounded p-3 bg-white text-center">
                                        <div class="preview-circle mx-auto shadow-sm" style="width: 150px; height: 150px; border-radius: 50%; overflow: hidden; border: 3px solid #198754; background: white;">
                                            <div id="cropperPreview" style="width: 100%; height: 100%;"></div>
                                        </div>
                                        <small class="text-muted d-block mt-2">Circle Preview</small>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer bg-light">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                        <i class="bi bi-x-circle me-1"></i>Cancel
                    </button>
                    <button type="button" class="btn btn-success px-4" id="cropAndSave">
                        <i class="bi bi-check-circle me-1"></i>Apply & Save
                    </button>
                </div>
            </div>
        </div>
    </div>`;
    }

    // Initialize controls when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupControls);
    } else {
        setupControls();
    }

    // Public API
    return {
        init: init,
        openCropper: openCropper,
        clearPhoto: clearPhoto,
        enlargeImage: enlargeImage,
        getModalHTML: getModalHTML
    };
})();

// Make it globally available
window.ImageCropper = ImageCropper;
