# Google reCAPTCHA v3 Setup Guide

## What Has Been Implemented

? **ReCaptchaService** - Service class to verify reCAPTCHA tokens with Google's API
? **Configuration** - Added ReCaptcha section in appsettings.json
? **Dependency Injection** - Registered ReCaptchaService in Program.cs
? **AccountController** - Updated Login method to verify reCAPTCHA on EVERY login attempt
? **Login View** - Integrated reCAPTCHA v3 (invisible) into the login form

## Important: reCAPTCHA Verification Strategy

**reCAPTCHA v3 will verify on EVERY login attempt**, even if the user has "Remember Me" checked.

### Why?
- "Remember Me" only pre-fills credentials from localStorage
- It does NOT mean the current login attempt is legitimate
- Bots can still submit forms with saved credentials
- reCAPTCHA v3 is invisible, so users won't notice any difference

### User Experience
? Seamless - No checkboxes or challenges for users
? Secure - Every login is validated against bot behavior
? "Remember Me" still works - Pre-fills email/password from localStorage

---

## Setup Instructions

### Step 1: Register Your Site with Google reCAPTCHA

1. Go to [Google reCAPTCHA Admin Console](https://www.google.com/recaptcha/admin/create)

2. Fill out the registration form:
   - **Label**: `Tuition Attendance System` (or any name you prefer)
   - **reCAPTCHA type**: Select **reCAPTCHA v3**
   - **Domains**: Add your domains
     - For development: `localhost`
     - For production: `yourdomain.com` (add your actual domain)
   
3. Accept the terms and click **Submit**

4. **Copy your keys**:
   - **Site Key** (public key - used in the frontend)
   - **Secret Key** (private key - used in the backend)

### Step 2: Configure Your Keys

1. Open `WebMobileAssignment/appsettings.json`

2. Replace the placeholder values with your actual keys:

```json
"ReCaptcha": {
  "SiteKey": "your-actual-site-key-here",
  "SecretKey": "your-actual-secret-key-here"
}
```

?? **IMPORTANT**: Never commit your Secret Key to a public repository!

### Step 3: Update appsettings.Development.json (Optional)

For better security in production, create separate keys for development and production:

1. Create development keys at Google reCAPTCHA Admin
2. Update `appsettings.Development.json`:

```json
{
  "ReCaptcha": {
    "SiteKey": "your-dev-site-key",
    "SecretKey": "your-dev-secret-key"
  }
}
```

---

## Testing the Implementation

### 1. Test Normal Login
- Navigate to `/Account/Login`
- Enter valid credentials
- Click "Login"
- **Expected**: Login succeeds (reCAPTCHA verifies in background)

### 2. Test With Remember Me
- Check the "Remember Me" checkbox
- Login successfully
- Close browser and reopen
- Navigate to `/Account/Login`
- **Expected**: Email/password are pre-filled, but reCAPTCHA still verifies on submit

### 3. Test Low Score Scenario
To test when reCAPTCHA gives a low score:
- You can temporarily lower the threshold in `AccountController.cs`:
  ```csharp
  var isRecaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken, "login", 0.9); // Higher threshold
  ```
- Rapid repeated submissions may trigger lower scores

### 4. Verify in Browser Console
Open browser DevTools (F12) ? Network tab:
- Look for the POST to `/Account/Login`
- Check that `recaptchaToken` is included in the form data

---

## How It Works

### Client-Side (Login.cshtml)
1. User fills out the login form
2. On form submit, JavaScript prevents default submission
3. `grecaptcha.execute()` is called to get a token from Google
4. Token is added to hidden input field `recaptchaToken`
5. Form is submitted with the token

### Server-Side (AccountController.cs)
1. Receives login request with `recaptchaToken`
2. Calls `ReCaptchaService.VerifyTokenAsync()` to verify with Google
3. Google returns a score (0.0 - 1.0) and action
4. If score ? 0.5 and action = "login", verification passes
5. Proceeds with normal login validation

---

## Adjusting Security Level

### Score Threshold
The default threshold is **0.5**. You can adjust this in `AccountController.cs`:

```csharp
// More lenient (allows more users, but may let some bots through)
var isRecaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken, "login", 0.3);

// More strict (blocks more bots, but may block some legitimate users)
var isRecaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken, "login", 0.7);
```

**Recommended**: Start with 0.5 and monitor your Google reCAPTCHA Admin dashboard for statistics.

---

## Monitoring & Analytics

### View Statistics
1. Go to [Google reCAPTCHA Admin Console](https://www.google.com/recaptcha/admin)
2. Select your site
3. View the dashboard showing:
   - Request volume
   - Score distribution
   - Suspicious activity

### Debugging
If verification fails, you can get detailed results:

```csharp
var details = await _reCaptchaService.GetVerificationDetailsAsync(recaptchaToken);
Console.WriteLine($"Success: {details.Success}, Score: {details.Score}, Action: {details.Action}");
```

---

## Troubleshooting

### Issue: "Security verification failed" message every time
**Possible Causes:**
1. Site Key or Secret Key is incorrect
2. Domain not registered in Google reCAPTCHA console
3. Keys are for a different reCAPTCHA version (v2 instead of v3)

**Solution:**
- Double-check your keys in `appsettings.json`
- Verify `localhost` is added to domains in Google console
- Ensure you selected "reCAPTCHA v3" when registering

### Issue: reCAPTCHA badge not appearing
**Solution:**
- The reCAPTCHA v3 badge should appear in the bottom-right corner
- If it doesn't, check browser console for JavaScript errors
- Verify the Site Key is correct in the view

### Issue: Login works without reCAPTCHA verification
**Possible Cause:** The reCAPTCHA script didn't load or token wasn't generated

**Solution:**
- Check Network tab in browser DevTools
- Ensure `https://www.google.com/recaptcha/api.js` loaded successfully
- Check if `recaptchaToken` field has a value before form submission

---

## Production Checklist

Before deploying to production:

- [ ] Register production domain in Google reCAPTCHA console
- [ ] Update production `appsettings.json` with production keys
- [ ] Never commit Secret Key to public repositories
- [ ] Consider using Azure Key Vault or environment variables for secrets
- [ ] Monitor reCAPTCHA dashboard for unusual activity
- [ ] Test login flow thoroughly on production domain

---

## Security Best Practices

1. **Never expose Secret Key**: Keep it server-side only
2. **Use different keys for dev/prod**: Helps with monitoring and security
3. **Monitor reCAPTCHA dashboard**: Watch for unusual patterns
4. **Adjust threshold based on data**: Use analytics to find the right balance
5. **Add rate limiting**: Consider additional protection for repeated login attempts

---

## Additional Features (Optional)

### Add reCAPTCHA to Other Forms
You can protect other forms like:
- Registration
- Password reset
- Contact forms

Just repeat the same pattern:
1. Add hidden input for token
2. Include reCAPTCHA script
3. Execute `grecaptcha.execute()` on form submit
4. Verify token in the controller

### Example for Registration:
```javascript
grecaptcha.execute('YOUR_SITE_KEY', {action: 'register'}).then(function(token) {
    document.getElementById('recaptchaToken').value = token;
    form.submit();
});
```

```csharp
var isValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken, "register", 0.5);
```

---

## Support Resources

- [Google reCAPTCHA Documentation](https://developers.google.com/recaptcha/docs/v3)
- [reCAPTCHA Admin Console](https://www.google.com/recaptcha/admin)
- [Best Practices Guide](https://developers.google.com/recaptcha/docs/v3#interpreting_the_score)

---

## Summary

? **reCAPTCHA v3 is now integrated** into your login system
? **Invisible protection** - Users won't see any challenges
? **Verifies every login** - Even with "Remember Me" enabled
? **Configurable** - Adjust threshold based on your needs
? **Production-ready** - Just add your keys and deploy

**Next Steps:**
1. Get your keys from Google reCAPTCHA Admin
2. Update `appsettings.json` with your keys
3. Test the login flow
4. Monitor the reCAPTCHA dashboard
5. Deploy to production!
