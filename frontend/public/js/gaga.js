document.addEventListener("DOMContentLoaded", () => {
    const hCaptchaResponse = "@(Model.hCaptchaResponse ?? "")";
    const div = document.getElementById("captchaDiv");
    div.addEventListener("click", () => {
        div.innerText = hCaptchaResponse;
    });
});
