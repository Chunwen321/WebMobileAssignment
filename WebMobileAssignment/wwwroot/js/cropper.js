/**
 * Image Cropper Module using Cropper.js
 * Provides image cropping functionality for profile picture uploads
 * Author: WebMobileAssignment
 */

const ImageCropper = (function() {
    let cropper = null;
    let currentFile = null;
    let onCropCompleteCallback = null;

    /**
     * Initialize the cropper modal and event handlers
     * @param {function} onCropComplete - Callback function to handle cropped file
     */
    function init(onCropComplete) {
        onCropCompleteCallback = onCropComplete || function() {};
        
        // Create cropper modal if it doesn't exist
        createCropperModal();
        
        // Setup event handlers
        setupEventHandlers();
    }

    /**
     * Create the cropper modal HTML
     */
    function createCropperModal() {
        // Check if modal already exists
     if (document.getElementById('cropperModal')) {
  return;
  }

   const modalHTML = `
            <div class="modal fade" id="cropperModal" tabindex="-1" aria-labelledby="cropperModalLabel" aria-hidden="true" data-bs-backdrop="static">
                <div class="modal-dialog modal-dialog-centered modal-lg">
         <div class="modal-content">
     <div class="modal-header bg-primary text-white">
            <h5 class="modal-title" id="cropperModalLabel">
     <i class="bi bi-crop me-2"></i>Crop Image
 </h5>
        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
    </div>
            <div class="modal-body">
               <div class="text-center mb-3">
              <img id="cropperImage" style="max-width: 100%; display: block;">
       </div>
                <div class="row g-3">
  <div class="col-12">
  <label class="form-label fw-bold">
        <i class="bi bi-zoom-in me-1"></i>Zoom
     </label>
        <input type="range" class="form-range" id="cropperZoom" min="-1" max="1" step="0.01" value="0">
              </div>
            </div>
       <div class="alert alert-info mt-3 mb-0">
    <i class="bi bi-info-circle me-2"></i>
        <small>
       <strong>Tips:</strong> Drag to reposition, use mouse wheel to zoom, or use the controls below
     </small>
           </div>
   </div>
     <div class="modal-footer">
           <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
      <i class="bi bi-x-circle me-1"></i>Cancel
    </button>
              <button type="button" class="btn btn-primary" id="btnRotateLeft">
        <i class="bi bi-arrow-counterclockwise me-1"></i>Rotate Left
         </button>
              <button type="button" class="btn btn-primary" id="btnRotateRight">
   <i class="bi bi-arrow-clockwise me-1"></i>Rotate Right
</button>
          <button type="button" class="btn btn-success" id="btnCropAndUpload">
       <i class="bi bi-check-circle me-1"></i>Crop & Save
  </button>
        </div>
  </div>
      </div>
   </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);
    }

    /**
     * Setup event handlers for cropper controls
     */
    function setupEventHandlers() {
        // Zoom control
        const zoomSlider = document.getElementById('cropperZoom');
        if (zoomSlider) {
       zoomSlider.addEventListener('input', function(e) {
  if (cropper) {
   cropper.zoomTo(parseFloat(e.target.value));
      }
   });
    }

 // Rotate left button
        const rotateLeftBtn = document.getElementById('btnRotateLeft');
        if (rotateLeftBtn) {
       rotateLeftBtn.addEventListener('click', function() {
          if (cropper) {
    cropper.rotate(-90);
     }
         });
        }

    // Rotate right button
        const rotateRightBtn = document.getElementById('btnRotateRight');
     if (rotateRightBtn) {
   rotateRightBtn.addEventListener('click', function() {
                if (cropper) {
        cropper.rotate(90);
    }
            });
 }

        // Crop and upload button
        const cropBtn = document.getElementById('btnCropAndUpload');
        if (cropBtn) {
     cropBtn.addEventListener('click', handleCropAndSave);
   }

        // Modal hidden event - cleanup
   const modal = document.getElementById('cropperModal');
      if (modal) {
         modal.addEventListener('hidden.bs.modal', function() {
    destroyCropper();
        });
        }
    }

    /**
     * Open cropper modal with an image file
     * @param {File} file - The image file to crop
     */
    function openCropperModal(file) {
      if (!file) {
            console.error('No file provided to cropper');
      return;
        }

        currentFile = file;
        const modal = new bootstrap.Modal(document.getElementById('cropperModal'));
        const image = document.getElementById('cropperImage');

        // Read file and display in modal
        const reader = new FileReader();
        reader.onload = function(e) {
 image.src = e.target.result;

       // Destroy existing cropper if any
     if (cropper) {
    cropper.destroy();
            }

            // Initialize Cropper.js
        cropper = new Cropper(image, {
                aspectRatio: 1, // Square crop for profile picture
    viewMode: 2,
    autoCropArea: 1,
       responsive: true,
        restore: true,
         guides: true,
              center: true,
       highlight: true,
                cropBoxMovable: true,
     cropBoxResizable: true,
          toggleDragModeOnDblclick: false,
        minContainerWidth: 200,
              minContainerHeight: 200,
  background: true,
              modal: true,
    scalable: true,
   zoomable: true,
    zoomOnWheel: true,
        wheelZoomRatio: 0.1
            });

      // Show modal
  modal.show();

    // Reset zoom slider
   const zoomSlider = document.getElementById('cropperZoom');
     if (zoomSlider) {
      zoomSlider.value = 0;
    }
        };

        reader.onerror = function() {
console.error('Failed to read file');
            alert('Failed to load image. Please try again.');
        };

    reader.readAsDataURL(file);
    }

    /**
     * Handle crop and save action
     */
    function handleCropAndSave() {
        if (!cropper) {
            console.error('Cropper not initialized');
   return;
 }

        // Get cropped canvas
        const canvas = cropper.getCroppedCanvas({
     width: 400,
            height: 400,
            minWidth: 200,
        minHeight: 200,
maxWidth: 800,
         maxHeight: 800,
       imageSmoothingEnabled: true,
    imageSmoothingQuality: 'high',
         fillColor: '#fff'
        });

    if (!canvas) {
 console.error('Failed to get cropped canvas');
            alert('Failed to crop image. Please try again.');
   return;
        }

        // Convert canvas to blob
        canvas.toBlob(function(blob) {
 if (!blob) {
          console.error('Failed to create blob from canvas');
     alert('Failed to process image. Please try again.');
        return;
   }

  // Create file from blob
      const croppedFile = new File([blob], currentFile.name, {
type: 'image/jpeg',
    lastModified: Date.now()
  });

            // Close modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('cropperModal'));
      if (modal) {
    modal.hide();
  }

            // Call callback with cropped file
   if (onCropCompleteCallback) {
   onCropCompleteCallback(croppedFile);
            }

        // Destroy cropper
      destroyCropper();

      }, 'image/jpeg', 0.92); // 92% quality for good balance
    }

    /**
     * Destroy cropper instance and cleanup
     */
    function destroyCropper() {
      if (cropper) {
            cropper.destroy();
         cropper = null;
        }
        currentFile = null;

        // Clear image src
        const image = document.getElementById('cropperImage');
        if (image) {
            image.src = '';
        }
    }

  /**
     * Get current cropper instance (for advanced usage)
 */
    function getCropper() {
        return cropper;
    }

    /**
     * Check if cropper is active
     */
    function isActive() {
        return cropper !== null;
    }

    // Public API
    return {
        init: init,
        openModal: openCropperModal,
        destroy: destroyCropper,
        getCropper: getCropper,
 isActive: isActive
    };
})();

// Make available globally
window.ImageCropper = ImageCropper;
