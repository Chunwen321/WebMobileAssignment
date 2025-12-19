/**
 * Webcam Capture Module
 * Provides webcam functionality for profile picture upload
 * Supports multiple independent webcam instances
 * Author: WebMobileAssignment
 */

const WebcamCapture = (function() {
    const instances = new Map(); // Store multiple webcam instances

    /**
     * Initialize webcam capture for a specific preview element
     * @param {string} previewId - ID of the preview element
     * @param {string} fileInputId - ID of the file input element
     * @param {function} onCaptureSuccess - Callback when photo is captured
     * @param {string} modalId - Optional custom modal ID (default: 'webcamModal')
     */
    function init(previewId, fileInputId, onCaptureSuccess, modalId = 'webcamModal') {
        const config = {
            previewId: previewId,
            fileInputId: fileInputId,
            modalId: modalId,
            onCaptureSuccess: onCaptureSuccess || function() {},
            stream: null,
            video: null,
            canvas: null,
            capturedBlob: null
        };

        // Store instance
        instances.set(modalId, config);

        createWebcamModal(config);
    }

    /**
     * Create the webcam modal HTML
     */
    function createWebcamModal(config) {
        const modalId = config.modalId;
        
        // Check if modal already exists
        if (document.getElementById(modalId)) {
            return;
        }

        const videoId = modalId + 'Video';
        const canvasId = modalId + 'Canvas';
        const loadingId = modalId + 'Loading';
        const errorId = modalId + 'Error';
        const errorMsgId = modalId + 'ErrorMessage';
        const captureBtnId = modalId + 'BtnCapture';
        const retakeBtnId = modalId + 'BtnRetake';
        const useBtnId = modalId + 'BtnUse';

        const modalHTML = `
            <div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-hidden="true" data-bs-backdrop="static">
                <div class="modal-dialog modal-dialog-centered modal-lg">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title" id="${modalId}Label">
                                <i class="bi bi-camera-fill me-2"></i>Take Photo
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="${modalId}Container" class="text-center">
                                <video id="${videoId}" autoplay playsinline class="img-fluid rounded mb-3" style="max-width: 100%; max-height: 400px; display: none;"></video>
                                <canvas id="${canvasId}" class="img-fluid rounded mb-3" style="max-width: 100%; max-height: 400px; display: none;"></canvas>
                                <div id="${loadingId}" class="text-center py-5">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                    <p class="mt-3 text-muted">Starting camera...</p>
                                </div>
                                <div id="${errorId}" class="alert alert-danger d-none">
                                    <i class="bi bi-exclamation-triangle-fill me-2"></i>
                                    <span id="${errorMsgId}"></span>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="bi bi-x-circle me-1"></i>Cancel
                            </button>
                            <button type="button" class="btn btn-warning" id="${retakeBtnId}" style="display: none;">
                                <i class="bi bi-arrow-counterclockwise me-1"></i>Retake
                            </button>
                            <button type="button" class="btn btn-primary" id="${captureBtnId}">
                                <i class="bi bi-camera me-1"></i>Capture Photo
                            </button>
                            <button type="button" class="btn btn-success" id="${useBtnId}" style="display: none;">
                                <i class="bi bi-check-circle me-1"></i>Use This Photo
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);
        
        // Initialize modal events
        const modal = document.getElementById(modalId);
        modal.addEventListener('shown.bs.modal', function() {
            startWebcam(config);
        });
        modal.addEventListener('hidden.bs.modal', function() {
            stopWebcam(config);
        });

        // Capture button
        document.getElementById(captureBtnId).addEventListener('click', function() {
            capturePhoto(config);
        });

        // Retake button
        document.getElementById(retakeBtnId).addEventListener('click', function() {
            retakePhoto(config);
        });

        // Use photo button
        document.getElementById(useBtnId).addEventListener('click', function() {
            usePhoto(config);
        });
    }

    /**
     * Start webcam
     */
    async function startWebcam(config) {
        const videoId = config.modalId + 'Video';
        const canvasId = config.modalId + 'Canvas';
        const loadingId = config.modalId + 'Loading';
        const errorId = config.modalId + 'Error';
        const errorMsgId = config.modalId + 'ErrorMessage';

        config.video = document.getElementById(videoId);
        config.canvas = document.getElementById(canvasId);
        const loading = document.getElementById(loadingId);
        const errorDiv = document.getElementById(errorId);
        const errorMsg = document.getElementById(errorMsgId);

        try {
            // Request camera access
            config.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: 'user'
                },
                audio: false
            });

            config.video.srcObject = config.stream;
            config.video.style.display = 'block';
            loading.style.display = 'none';
            errorDiv.classList.add('d-none');

        } catch (err) {
            console.error('Camera access error:', err);
            loading.style.display = 'none';
            errorDiv.classList.remove('d-none');
            
            if (err.name === 'NotAllowedError') {
                errorMsg.textContent = 'Camera access denied. Please allow camera access in your browser settings.';
            } else if (err.name === 'NotFoundError') {
                errorMsg.textContent = 'No camera found. Please connect a camera and try again.';
            } else {
                errorMsg.textContent = 'Failed to access camera: ' + err.message;
            }
        }
    }

    /**
     * Stop webcam
     */
    function stopWebcam(config) {
        if (config.stream) {
            config.stream.getTracks().forEach(track => track.stop());
            config.stream = null;
        }
        if (config.video) {
            config.video.srcObject = null;
        }
        config.capturedBlob = null;
    }

    /**
     * Capture photo from webcam
     */
    function capturePhoto(config) {
        if (!config.video) return;

        const captureBtnId = config.modalId + 'BtnCapture';
        const retakeBtnId = config.modalId + 'BtnRetake';
        const useBtnId = config.modalId + 'BtnUse';

        // Set canvas size to match video
        config.canvas.width = config.video.videoWidth;
        config.canvas.height = config.video.videoHeight;

        // Draw video frame to canvas
        const ctx = config.canvas.getContext('2d');
        ctx.drawImage(config.video, 0, 0, config.canvas.width, config.canvas.height);

        // Hide video, show canvas
        config.video.style.display = 'none';
        config.canvas.style.display = 'block';

        // Update buttons
        document.getElementById(captureBtnId).style.display = 'none';
        document.getElementById(retakeBtnId).style.display = 'inline-block';
        document.getElementById(useBtnId).style.display = 'inline-block';

        // Convert canvas to blob
        config.canvas.toBlob(function(blob) {
            config.capturedBlob = blob;
        }, 'image/jpeg', 0.9);
    }

    /**
     * Retake photo
     */
    function retakePhoto(config) {
        const captureBtnId = config.modalId + 'BtnCapture';
        const retakeBtnId = config.modalId + 'BtnRetake';
        const useBtnId = config.modalId + 'BtnUse';

        config.canvas.style.display = 'none';
        config.video.style.display = 'block';

        // Update buttons
        document.getElementById(captureBtnId).style.display = 'inline-block';
        document.getElementById(retakeBtnId).style.display = 'none';
        document.getElementById(useBtnId).style.display = 'none';

        config.capturedBlob = null;
    }

    /**
     * Use captured photo
     */
    function usePhoto(config) {
        if (!config.capturedBlob) return;

        const preview = document.getElementById(config.previewId);
        const fileInput = document.getElementById(config.fileInputId);

        // Create a File object from the blob
        const file = new File([config.capturedBlob], `webcam-photo-${Date.now()}.jpg`, {
            type: 'image/jpeg',
            lastModified: Date.now()
        });

        // Create a DataTransfer object to set the file input
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        fileInput.files = dataTransfer.files;

        // Update preview
        const reader = new FileReader();
        reader.onload = function(e) {
            if (preview.tagName === 'IMG') {
                preview.src = e.target.result;
                // Add click-to-enlarge functionality
                preview.style.cursor = 'pointer';
                preview.setAttribute('onclick', `enlargeImage(this.src)`);
                preview.setAttribute('title', 'Click to enlarge');
            } else {
                // Determine size based on preview element size
                const size = preview.style.width || '200px';
                const fontSize = size === '150px' ? '7.5rem' : '10rem';
                
                // Replace icon div with image with click-to-enlarge
                preview.outerHTML = `<img id="${config.previewId}" 
                                         src="${e.target.result}" 
                                         alt="Profile Picture" 
                                         class="img-thumbnail rounded-circle mb-3" 
                                         style="width: ${size}; height: ${size}; object-fit: cover; cursor: pointer;"
                                         onclick="enlargeImage(this.src)"
                                         title="Click to enlarge">`;
            }

            // Show clear button if exists (try both possible IDs)
            const clearBtn = document.getElementById('clearProfilePictureBtn') || 
                           document.getElementById('clearParentProfilePictureBtn');
            if (clearBtn) {
                clearBtn.style.display = 'block';
            }

            // Call success callback
            config.onCaptureSuccess(file);
            
            // Note: Photo is now ready in the file input
            // It will be uploaded when the user submits the form
            // We do NOT trigger the change event here to avoid immediate upload
        };
        reader.readAsDataURL(file);

        // Close modal
        const modal = bootstrap.Modal.getInstance(document.getElementById(config.modalId));
        modal.hide();
    }

    /**
     * Open webcam modal
     * @param {string} modalId - Optional modal ID (default: 'webcamModal')
     */
    function openModal(modalId = 'webcamModal') {
        const modalElement = document.getElementById(modalId);
        if (modalElement) {
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        } else {
            console.error(`Modal with ID "${modalId}" not found`);
        }
    }

    /**
     * Stop all webcam streams
     */
    function stopAllWebcams() {
        instances.forEach((config) => {
            stopWebcam(config);
        });
    }

    // Public API
    return {
        init: init,
        openModal: openModal,
        stopAllWebcams: stopAllWebcams
    };
})();

// Make available globally
window.WebcamCapture = WebcamCapture;
