window.otpTimer = {
    start: function (duration, elementId, resendButtonId) {
        let timer = duration, minutes, seconds;
        const display = document.querySelector('#' + elementId);
        const resendButton = document.querySelector('#' + resendButtonId);

        // Disable resend button at the start
        if (resendButton) {
            resendButton.disabled = true;
        }

        const interval = setInterval(function () {
            minutes = parseInt(timer / 60, 10);
            seconds = parseInt(timer % 60, 10);

            minutes = minutes < 10 ? "0" + minutes : minutes;
            seconds = seconds < 10 ? "0" + seconds : seconds;

            if (display) {
                display.textContent = "Resend OTP in " + minutes + ":" + seconds;
            }

            if (--timer < 0) {
                clearInterval(interval);
                if (display) {
                    display.textContent = "";
                }
                // Enable resend button when timer finishes
                if (resendButton) {
                    resendButton.disabled = false;
                }
            }
        }, 1000);
    }
};