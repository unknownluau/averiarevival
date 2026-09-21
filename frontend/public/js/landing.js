window.onLoginCaptchaSuccess = function(token) {
    const loginForm = document.getElementById('login-form');
    let input = loginForm.querySelector('input[name="cf-turnstile-response"]');
    if (!input) {
        input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'cf-turnstile-response';
        loginForm.appendChild(input);
    }
    input.value = token;
    document.getElementById('login-captcha-overlay').classList.remove('active');
    loginForm.submit();
};

document.addEventListener('DOMContentLoaded', function() {
    const loginBtn = document.getElementById('loginBtn');
    const overlay = document.getElementById('login-captcha-overlay');
    const closeBtn = document.getElementById('captchaCloseBtn');

    if (loginBtn) {
        loginBtn.addEventListener('click', function(e) {
            e.preventDefault();
            overlay.classList.add('active');
            if (typeof turnstile !== 'undefined') {
                turnstile.reset('#login-turnstile-container');
            }
        });
    }

    if (closeBtn) {
        closeBtn.addEventListener('click', function() {
            overlay.classList.remove('active');
        });
    }

    if (overlay) {
        overlay.addEventListener('click', function(e) {
            if (e.target === this) {
                this.classList.remove('active');
            }
        });
    }
});