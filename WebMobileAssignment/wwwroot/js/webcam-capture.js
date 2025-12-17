/**
 * Webcam Capture Module
 * Provides webcam functionality for profile picture upload
 * Author: WebMobileAssignment
 */

const WebcamCapture = (function() {
    let stream = null;
    let video = null;
    let canvas = null;
    let capturedBlob = null;

    /**
     * Initialize webcam capture for a specific preview element
     * @param {string} previewId - ID of the preview element
     * @param {string} fileInputId - ID of the file input element
     * @param {function} onCaptureSuccess - Callback when photo is captured
     */
    function init(previewId, fileInputId, onCaptureSuccess) {
        const config = {
            previewId: previewId,
            fileInputId: fileInputId,
            onCaptureSuccess: onCaptureSuccess || function() {}
        };

        createWebcamModal(config);
    }

    /**
     * Create the webcam modal HTML
     */
    function createWebcamModal(config) {
        // Check if modal already exists
        if (document.getElementById('webcamModal')) {
            return;
        }

        const modalHTML = `
            <div class="modal fade" id="webcamModal" tabindex="-1" aria-labelledby="webcamModalLabel" aria-hidden="true" data-bs-backdrop="static">
                <div class="modal-dialog modal-dialog-centered modal-lg">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title" id="webcamModalLabel">
                                <i class="bi bi-camera-fill me-2"></i>Take Photo
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="webcamContainer" class="text-center">
                                <video id="webcamVideo" autoplay playsinline class="img-fluid rounded mb-3" style="max-width: 100%; max-height: 400px; display: none;"></video>
                                <canvas id="webcamCanvas" class="img-fluid rounded mb-3" style="max-width: 100%; max-height: 400px; display: none;"></canvas>
                                <div id="webcamLoading" class="text-center py-5">
                                    <div class="spinner-border text-primary" role="status">
                                        <span class="visually-hidden">Loading...</span>
                                    </div>
                                    <p class="mt-3 text-muted">Starting camera...</p>
                                </div>
                                <div id="webcamError" class="alert alert-danger d-none">
                                    <i class="bi bi-exclamation-triangle-fill me-2"></i>
                                    <span id="webcamErrorMessage"></span>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="bi bi-x-circle me-1"></i>Cancel
                            </button>
                            <button type="button" class="btn btn-warning" id="btnRetake" style="display: none;">
                                <i class="bi bi-arrow-counterclockwise me-1"></i>Retake
                            </button>
                            <button type="button" class="btn btn-primary" id="btnCapture">
                                <i class="bi bi-camera me-1"></i>Capture Photo
                            </button>
                            <button type="button" class="btn btn-success" id="btnUsePhoto" style="display: none;">
                                <i class="bi bi-check-circle me-1"></i>Use This Photo
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);
        
        // Initialize modal events
        const modal = document.getElementById('webcamModal');
        modal.addEventListener('shown.bs.modal', function() {
            startWebcam(config);
        });
        modal.addEventListener('hidden.bs.modal', function() {
            stopWebcam();
        });

        // Capture button
        document.getElementById('btnCapture').addEventListener('click', function() {
            capturePhoto(config);
        });

        // Retake button
        document.getElementById('btnRetake').addEventListener('click', function() {
            retakePhoto(config);
        });

        // Use photo button
        document.getElementById('btnUsePhoto').addEventListener('click', function() {
            usePhoto(config);
        });
    }

    /**
     * Start webcam
     */
    async function startWebcam(config) {
        video = document.getElementById('webcamVideo');
        canvas = document.getElementById('webcamCanvas');
        const loading = document.getElementById('webcamLoading');
        const errorDiv = document.getElementById('webcamError');
        const errorMsg = document.getElementById('webcamErrorMessage');

        try {
            // Request camera access
            stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: 'user'
                },
                audio: false
            });

            video.srcObject = stream;
            video.style.display = 'block';
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
    function stopWebcam() {
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            stream = null;
        }
        if (video) {
            video.srcObject = null;
        }
        capturedBlob = null;
    }

    /**
     * Capture photo from webcam
     */
    function capturePhoto(config) {
        if (!video) return;

        // Set canvas size to match video
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        // Draw video frame to canvas
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Hide video, show canvas
        video.style.display = 'none';
        canvas.style.display = 'block';

        // Update buttons
        document.getElementById('btnCapture').style.display = 'none';
        document.getElementById('btnRetake').style.display = 'inline-block';
        document.getElementById('btnUsePhoto').style.display = 'inline-block';

        // Convert canvas to blob
        canvas.toBlob(function(blob) {
            capturedBlob = blob;
        }, 'image/jpeg', 0.9);
    }

    /**
     * Retake photo
     */
    function retakePhoto(config) {
        canvas.style.display = 'none';
        video.style.display = 'block';

        // Update buttons
        document.getElementById('btnCapture').style.display = 'inline-block';
        document.getElementById('btnRetake').style.display = 'none';
        document.getElementById('btnUsePhoto').style.display = 'none';

        capturedBlob = null;
    }

    /**
     * Use captured photo
     */
    function usePhoto(config) {
        if (!capturedBlob) return;

        const preview = document.getElementById(config.previewId);
        const fileInput = document.getElementById(config.fileInputId);

        // Create a File object from the blob
        const file = new File([capturedBlob], `webcam-photo-${Date.now()}.jpg`, {
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
            } else {
                // Replace icon div with image
                preview.outerHTML = `<img id="${config.previewId}" 
                                         src="${e.target.result}" 
                                         alt="Profile Picture" 
                                         class="img-thumbnail rounded-circle mb-3" 
                                         style="width: 200px; height: 200px; object-fit: cover;">`;
            }

            // Show clear button if exists
            const clearBtn = document.getElementById('clearProfilePictureBtn');
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
        const modal = bootstrap.Modal.getInstance(document.getElementById('webcamModal'));
        modal.hide();
    }

    /**
     * Open webcam modal
     */
    function openModal() {
        const modal = new bootstrap.Modal(document.getElementById('webcamModal'));
        modal.show();
    }

    // Public API
    return {
        init: init,
        openModal: openModal,
        stopWebcam: stopWebcam
    };
})();

// Make available globally
window.WebcamCapture = WebcamCapture;
