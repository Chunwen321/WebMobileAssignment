# ?? reCAPTCHA v3 Troubleshooting Guide

## Current Configuration
- Site Key: `6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt`
- Secret Key: `6Ld3LiwsAAAAAOe8VfoANfdy0gT9o2GiKgtE37n6`

---

## ?? Step-by-Step Diagnostic Procedure

### Step 1: Restart Everything
1. **Stop Visual Studio Debugging** (Shift + F5)
2. **Close all browser tabs**
3. **Rebuild Solution** (Ctrl + Shift + B)
4. **Start Debugging** (F5)

### Step 2: Clear Browser Cache
1. Press `Ctrl + Shift + Delete`
2. Select "Cached images and files"
3. Click "Clear data"
4. **OR** just use `Ctrl + F5` for hard refresh

### Step 3: Open Browser Console
1. Navigate to: `https://localhost:7079/Account/Login`
2. Press **F12** to open Developer Tools
3. Click **Console** tab
4. Keep it open while testing

### Step 4: Check Page Source
1. Right-click on page ? "View Page Source" (Ctrl + U)
2. Search for: `ReCaptchaSiteKey`
3. **Expected to see:**
   ```html
   <script src="https://www.google.com/recaptcha/api.js?render=6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt"></script>
   ```
4. **If NOT present**: ViewBag.ReCaptchaSiteKey is empty

---

## ?? Diagnostic Tests

### Test 1: Check if reCAPTCHA Script Loads

**In Browser Console, type:**
```javascript
typeof grecaptcha
```

**Expected Result:**
- ? `"object"` ? reCAPTCHA loaded successfully
- ? `"undefined"` ? reCAPTCHA script didn't load

---

### Test 2: Check ViewBag Value

**In Browser Console, check page source for:**
```javascript
var siteKey = '6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt';
```

**If you see:**
- ? `'6Ld3LiwsAAAAA...'` ? Site key is passed correctly
- ? `''` or `'YOUR_SITE_KEY_HERE'` ? Configuration issue

---

### Test 3: Manually Execute reCAPTCHA

**In Browser Console, type:**
```javascript
grecaptcha.ready(function() {
    grecaptcha.execute('6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt', {action: 'login'}).then(function(token) {
  console.log('? Token received:', token);
  });
});
```

**Expected Result:**
- ? You see: `? Token received: 03AHJ_Vuv...`
- ? Error message ? Check error details

---

### Test 4: Check Network Tab

1. **Open Network tab** in DevTools (F12)
2. **Filter by "recaptcha"**
3. **Try to login**

**Expected Requests:**
- ? `api.js?render=6Ld3...` (Status: 200)
- ? `api2/reload?k=6Ld3...` (Status: 200)
- ? `api2/userverify?k=6Ld3...` (Status: 200)

**If you see:**
- ? No requests ? Script not loading
- ? 404 errors ? Wrong Site Key or network issue
- ? 400 errors ? Invalid key or domain mismatch

---

## ?? Common Issues & Solutions

### Issue 1: "reCAPTCHA not configured or unavailable"

**Console shows:**
```
?? reCAPTCHA not configured or unavailable - proceeding without verification
```

**Possible Causes:**
1. reCAPTCHA script didn't load
2. ViewBag.ReCaptchaSiteKey is null/empty
3. JavaScript error preventing execution

**Solutions:**
```csharp
// Add this to Login() GET method in AccountController.cs
public IActionResult Login()
{
    ViewBag.ReCaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];
    
    // DEBUG: Print to console
    Console.WriteLine($"?? SiteKey being passed: {ViewBag.ReCaptchaSiteKey}");
    
    return View();
}
```

---

### Issue 2: Script Loads But Nothing Happens

**Check for JavaScript Errors:**
1. Open Console tab
2. Look for red error messages
3. Common errors:
   - `Uncaught ReferenceError: grecaptcha is not defined`
   - `Invalid site key or not loaded in current domain`

**Solution:**
- Verify domain registered in Google reCAPTCHA: `localhost` (not `https://localhost:7079`)
- Check Site Key is correct
- Ensure script has time to load (async loading issue)

---

### Issue 3: Form Submits Without reCAPTCHA

**This means:**
- JavaScript condition is false
- `typeof grecaptcha` is `undefined`

**Debug:**
Add this before form submit:
```javascript
console.log('SiteKey:', siteKey);
console.log('grecaptcha exists?', typeof grecaptcha !== 'undefined');
console.log('Condition result:', siteKey && siteKey !== 'YOUR_SITE_KEY_HERE' && siteKey !== '' && typeof grecaptcha !== 'undefined');
```

---

### Issue 4: "Security verification failed"

**Good news: reCAPTCHA IS working!**

**Possible reasons:**
1. Score too low (< 0.5)
2. Server-side verification failed
3. Wrong Secret Key
4. Network issue contacting Google

**Check Server Output:**
Look for these messages in Visual Studio Output window:
```
? reCAPTCHA verification passed for email: user@example.com
```
OR
```
?? Warning: reCAPTCHA token is missing for login attempt: user@example.com
```

---

## ?? Complete Diagnostic Checklist

Run through this checklist in order:

### Client-Side Checks:
- [ ] **F12 Console open** - No JavaScript errors
- [ ] **Console shows:** "?? Executing reCAPTCHA v3 verification..."
- [ ] **Console shows:** "? reCAPTCHA token received: ..."
- [ ] **Network tab** shows requests to google.com/recaptcha
- [ ] **reCAPTCHA badge visible** in bottom-right corner
- [ ] **Page source** contains `api.js?render=6Ld3LiwsAAAAA...`
- [ ] **Browser Console:** `typeof grecaptcha` returns `"object"`

### Server-Side Checks:
- [ ] **appsettings.json** has correct Site Key and Secret Key
- [ ] **Visual Studio Output** shows: "?? SiteKey being passed: 6Ld3LiwsAAAAA..."
- [ ] **No build errors** in Error List window
- [ ] **Application restarted** after changing appsettings.json

### Google reCAPTCHA Admin:
- [ ] **Logged in to:** https://www.google.com/recaptcha/admin
- [ ] **Site registered** as reCAPTCHA v3 (NOT v2!)
- [ ] **Domain listed:** `localhost` (not full URL)
- [ ] **Keys match** what's in appsettings.json

---

## ?? Quick Verification Commands

### In Browser Console:

**1. Check if script loaded:**
```javascript
console.log('reCAPTCHA loaded?', typeof grecaptcha !== 'undefined');
```

**2. Get site key from page:**
```javascript
console.log('Site key:', document.querySelector('script[src*="recaptcha"]')?.src);
```

**3. Check form element:**
```javascript
console.log('Form exists?', document.getElementById('loginForm') !== null);
console.log('Hidden input exists?', document.getElementById('recaptchaToken') !== null);
```

**4. Test token generation:**
```javascript
if (typeof grecaptcha !== 'undefined') {
    grecaptcha.ready(function() {
        grecaptcha.execute('6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt', {action: 'test'})
            .then(token => console.log('? Test token:', token))
    .catch(err => console.error('? Error:', err));
  });
} else {
    console.error('? grecaptcha not loaded');
}
```

---

## ?? Advanced Debugging

### Enable Detailed Logging

**Add to Login.cshtml Script section:**
```javascript
// At the start of DOMContentLoaded
console.log('=== reCAPTCHA Debug Info ===');
console.log('ViewBag.ReCaptchaSiteKey:', '@ViewBag.ReCaptchaSiteKey');
console.log('Script element:', document.querySelector('script[src*="recaptcha"]'));
console.log('grecaptcha available:', typeof grecaptcha !== 'undefined');
console.log('Form element:', document.getElementById('loginForm'));
console.log('Token input:', document.getElementById('recaptchaToken'));
console.log('=========================');
```

### Add Server-Side Detailed Logging

**In AccountController Login method:**
```csharp
[HttpPost]
public async Task<IActionResult> Login(string email, string password, string? recaptchaToken, bool rememberMe = false)
{
    ViewBag.ReCaptchaSiteKey = _configuration["ReCaptcha:SiteKey"];

    // DETAILED LOGGING
 Console.WriteLine("=== Login Attempt Debug ===");
    Console.WriteLine($"Email: {email}");
    Console.WriteLine($"reCAPTCHA Token received: {(string.IsNullOrEmpty(recaptchaToken) ? "EMPTY" : "YES - " + recaptchaToken.Substring(0, 20) + "...")}");
    Console.WriteLine($"Token length: {recaptchaToken?.Length ?? 0}");
    Console.WriteLine($"Site Key configured: {_configuration["ReCaptcha:SiteKey"]}");
    Console.WriteLine($"Secret Key configured: {(!string.IsNullOrEmpty(_configuration["ReCaptcha:SecretKey"]) ? "YES" : "NO")}");
    
    // ... rest of your code
}
```

---

## ?? If Still Not Working

### Scenario 1: Console Shows "?? reCAPTCHA not configured..."

**Try this temporary fix:**
Replace the form submission handler in Login.cshtml with:
```javascript
document.getElementById('loginForm').addEventListener('submit', function(e) {
    e.preventDefault(); // ALWAYS prevent default first
    
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');
    const rememberMeCheckbox = document.getElementById('rememberMe');
    
    // Save credentials
    if (rememberMeCheckbox.checked) {
        localStorage.setItem('rememberedEmail', emailInput.value);
        localStorage.setItem('rememberedPassword', passwordInput.value);
  localStorage.setItem('rememberMe', 'true');
    } else {
localStorage.removeItem('rememberedEmail');
        localStorage.removeItem('rememberedPassword');
        localStorage.removeItem('rememberMe');
 }
    
    var siteKey = '@ViewBag.ReCaptchaSiteKey';
    console.log('DEBUG: siteKey =', siteKey);
    console.log('DEBUG: siteKey type =', typeof siteKey);
    console.log('DEBUG: siteKey length =', siteKey.length);
  console.log('DEBUG: grecaptcha exists =', typeof grecaptcha !== 'undefined');
    
    // Force execution regardless of conditions (for testing)
    if (typeof grecaptcha !== 'undefined') {
   console.log('?? FORCING reCAPTCHA execution...');
        grecaptcha.ready(function() {
            grecaptcha.execute('@ViewBag.ReCaptchaSiteKey', {action: 'login'})
  .then(function(token) {
           console.log('? Token:', token.substring(0, 30) + '...');
          document.getElementById('recaptchaToken').value = token;
  e.target.submit();
  })
             .catch(function(error) {
      console.error('? reCAPTCHA Error:', error);
          alert('reCAPTCHA error: ' + error);
      });
        });
    } else {
        console.error('? grecaptcha NOT LOADED');
        alert('reCAPTCHA script not loaded. Check console for details.');
    }
});
```

---

### Scenario 2: Google Dashboard Shows 0 Verifications

**This confirms client-side is NOT working.**

**Checklist:**
1. Verify you're on the reCAPTCHA v3 site (not v2)
2. Check domain: should be just `localhost`
3. Copy keys directly from Google dashboard again
4. Paste into appsettings.json (watch for extra spaces!)
5. **Save appsettings.json**
6. **Restart application**
7. Hard refresh browser (Ctrl + F5)

---

## ?? What Success Looks Like

### Browser Console (Success):
```
?? Executing reCAPTCHA v3 verification...
? reCAPTCHA token received: 03AHJ_VuvHpN8fgW_jY...
?? Submitting form with reCAPTCHA token
```

### Visual Studio Output (Success):
```
?? SiteKey being passed: 6Ld3LiwsAAAAACOVh2qZOqIc-wsi2tpmo--4UdTt
? reCAPTCHA verification passed for email: user@example.com
```

### Google Dashboard (Success):
- **Total verifications:** Increasing number
- **Graph showing:** Request activity
- **Score distribution:** Data displayed

### Visual Confirmation (Success):
- ? Small reCAPTCHA badge in bottom-right corner
- ? No console errors
- ? Login works smoothly
- ? Network tab shows Google reCAPTCHA requests

---

## ?? Next Steps

1. **Run through the checklist above**
2. **Take screenshots of:**
   - Browser console output
   - Visual Studio Output window
   - Google reCAPTCHA dashboard
   - Network tab (filtered by "recaptcha")
3. **Note what you see at each step**
4. **Report back with findings**

The diagnostic information will help pinpoint exactly where the issue is!
