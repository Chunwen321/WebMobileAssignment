# Instructions for Updating Forms to Use Image Cropper Module

## Steps to Update Each Form

### 1. Add Script References (in @section Scripts)
Replace the existing cropper scripts with:

```razor
<!-- Cropper.js CSS and JS -->
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.css" />
<script src="https://cdnjs.cloudflare.com/ajax/libs/cropperjs/1.6.1/cropper.min.js"></script>

<!-- Webcam Capture Script -->
<script src="~/js/webcam-capture.js"></script>

<!-- Image Cropper Module -->
<script src="~/js/image-cropper.js"></script>
```

### 2. Add Modal HTML (before closing @section Scripts)
Add this line to inject the modals:

```razor
@Html.Raw(await Html.PartialAsync("_ImageCropperModals"))
```

OR manually add:
```html
<div id="imageCropperModals"></div>
<script>
    document.getElementById('imageCropperModals').innerHTML = ImageCropper.getModalHTML();
</script>
```

### 3. Initialize Image Cropper (replace all existing cropper JavaScript)

**For AddStudent.cshtml - Student Photo:**
```javascript
<script>
    // Initialize ImageCropper for student
    ImageCropper.init({
        targetId: 'student',
        previewElementId: 'profilePicturePreview',
        fileInputId: 'profilePicture',
        clearButtonId: 'clearProfilePictureBtn',
        editButtonId: 'btnEditPhoto',
        previewWidth: 200,
        previewHeight: 200
    });

    // Initialize ImageCropper for parent
    ImageCropper.init({
        targetId: 'parent',
        previewElementId: 'parentProfilePicturePreview',
        fileInputId: 'parentProfilePicture',
        clearButtonId: 'clearParentProfilePictureBtn',
        previewWidth: 150,
        previewHeight: 150
    });

    // Keep existing webcam, parent search, and class selection code
    // ... (rest of your existing code for parent search and class selection)
</script>
```

**For StudentEdit.cshtml:**
```javascript
<script>
    // Initialize ImageCropper for student
    ImageCropper.init({
        targetId: 'student',
        previewElementId: 'profilePicturePreview',
        fileInputId: 'profilePicture',
        clearButtonId: 'clearProfilePictureBtn',
        editButtonId: 'btnEditPhoto',
        previewWidth: 200,
        previewHeight: 200
    });
</script>
```

**For TeacherCreate.cshtml:**
```javascript
<script>
    // Initialize ImageCropper for teacher
    ImageCropper.init({
        targetId: 'teacher',
        previewElementId: 'teacherProfilePicturePreview',
        fileInputId: 'teacherProfilePicture',
        clearButtonId: 'clearTeacherProfilePictureBtn',
        editButtonId: 'btnEditPhoto',
        previewWidth: 200,
        previewHeight: 200
    });
</script>
```

**For ParentCreate.cshtml:**
```javascript
<script>
    // Initialize ImageCropper for parent
    ImageCropper.init({
        targetId: 'parent',
        previewElementId: 'parentProfilePicturePreview',
        fileInputId: 'parentProfilePicture',
        clearButtonId: 'clearParentProfilePictureBtn',
        editButtonId: 'btnEditPhoto',
        previewWidth: 200,
        previewHeight: 200
    });
</script>
```

**For ParentEdit.cshtml:**
```javascript
<script>
    // Initialize ImageCropper for parent
    ImageCropper.init({
        targetId: 'parent',
        previewElementId: 'parentProfilePicturePreview',
        fileInputId: 'parentProfilePicture',
        clearButtonId: 'clearParentProfilePictureBtn',
        editButtonId: 'btnEditPhoto',
        previewWidth: 200,
        previewHeight: 200
    });
</script>
```

### 4. Remove Old Code
Delete these from all forms:
- All the old `initImageCropper()` function
- All the old `updatePreview()` function
- All the old zoom, rotate, flip control JavaScript
- All the old `updateStudentPhoto()` / `updateParentPhoto()` / `updateTeacherPhoto()` functions
- All the old `enlargeImage()` function
- All the old `clearProfilePicture()` / `clearParentProfilePicture()` functions
- All the old modal HTML (keep only the modals injected by the module)

### 5. Update onclick Handlers
Change any inline onclick handlers like:
```html
<!-- OLD -->
onclick="clearProfilePicture()"

<!-- NEW (remove onclick entirely, handled by module) -->
```

The clear buttons no longer need onclick handlers as the module handles them automatically.

## Summary of Changes Per File

### AddStudent.cshtml
- Replace ~500 lines of cropper JavaScript with ~20 lines
- Initialize both student and parent croppers
- Keep parent search and class selection code

### StudentEdit.cshtml
- Replace ~300 lines of cropper JavaScript with ~10 lines
- Initialize student cropper only

### TeacherCreate.cshtml  
- Add ~15 lines of initialization code
- Add file input onchange handler

### ParentCreate.cshtml
- Replace existing code with ~10 lines
- Initialize parent cropper

### ParentEdit.cshtml
- Replace existing code with ~10 lines
- Initialize parent cropper

## Benefits
? Code reduced by ~80%
? Consistent behavior across all forms
? Easier to maintain and update
? Single source of truth for cropper functionality
? Better separation of concerns
