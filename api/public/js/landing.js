var cfg = document.getElementById('landing-config');
var siteKey = cfg.dataset.sitekey;
var autoOpenLogin = cfg.dataset.autoopenlogin === 'true';

function openLoginModal() {
    document.getElementById('loginModalOverlay').style.display = 'flex';
}

document.getElementById('openLoginModal').addEventListener('click', openLoginModal);
document.getElementById('alreadyHaveAccount').addEventListener('click', function (e) {
    e.preventDefault();
    openLoginModal();
});
document.getElementById('closeLoginModal').addEventListener('click', function () {
    document.getElementById('loginModalOverlay').style.display = 'none';
});
document.getElementById('loginModalOverlay').addEventListener('click', function (e) {
    if (e.target === this) this.style.display = 'none';
});

if (autoOpenLogin) {
    document.getElementById('loginModalOverlay').style.display = 'flex';
}

var pendingForm = null;
var pendingResponseField = null;
var turnstileRendered = false;

function showRobotCheck(formEl, hiddenField) {
    pendingForm = formEl;
    pendingResponseField = hiddenField;
    document.getElementById('robotCheckOverlay').style.display = 'flex';

    if (!turnstileRendered) {
        turnstile.render('#turnstileWidget', {
            sitekey: siteKey,
            theme: 'light',
            callback: function (token) {
                pendingResponseField.value = token;
                document.getElementById('robotCheckOverlay').style.display = 'none';
                pendingForm.submit();
            },
            'error-callback': function () {
                document.getElementById('robotCheckOverlay').style.display = 'none';
                turnstileRendered = false;
            }
        });
        turnstileRendered = true;
    } else {
        turnstile.reset('#turnstileWidget');
    }
}

document.getElementById('signupSubmitBtn').addEventListener('click', function () {
    showRobotCheck(
        document.getElementById('signupForm'),
        document.getElementById('signupCaptchaResponse')
    );
});

document.getElementById('loginSubmitBtn').addEventListener('click', function () {
    showRobotCheck(
        document.getElementById('loginForm'),
        document.getElementById('loginCaptchaResponse')
    );
});
