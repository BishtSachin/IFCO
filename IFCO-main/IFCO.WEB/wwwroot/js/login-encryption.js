function pemToArrayBuffer(pem) {
    const b64 = pem.replace(/-----BEGIN PUBLIC KEY-----/g, "")
        .replace(/-----END PUBLIC KEY-----/g, "")
        .replace(/[\n\r]/g, "");

    const binaryString = window.atob(b64);
    const len = binaryString.length;
    const bytes = new Uint8Array(len);
    for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes.buffer;
}

async function encryptWithWebCrypto(plainText, pemPublicKey) {
    try {
        const binaryKey = pemToArrayBuffer(pemPublicKey);

        const key = await window.crypto.subtle.importKey(
            "spki",
            binaryKey,
            {
                name: "RSA-OAEP",
                hash: "SHA-256"
            },
            false,
            ["encrypt"]
        );

        const encoder = new TextEncoder();
        const data = encoder.encode(plainText);

        const encryptedBuffer = await window.crypto.subtle.encrypt(
            { name: "RSA-OAEP" },
            key,
            data
        );

        let binary = '';
        const bytes = new Uint8Array(encryptedBuffer);
        const len = bytes.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);

    } catch (e) {
        console.error("Web Crypto Error:", e);
        return null;
    }
}
async function performEncryptedLogin(loginType, userId, captchaQuestion, captchaAnswer)
{
    try
    {
        const formData = new FormData();
        formData.append('LoginType', loginType);
        formData.append('UserId', userId);
        formData.append('CaptchaQuestion', captchaQuestion);
        formData.append('UserCaptchaAnswer', captchaAnswer);

        const keyResponse = await fetch('api/auth/public-key');
        if (!keyResponse.ok) throw new Error('Could not fetch public key.');
        const keyData = await keyResponse.json();
        const publicKeyPem = keyData.publicKey;

        let encryptedValue = null;

        if (loginType === 'Consultant')
        {
            const mobileInput = document.getElementById('mobileNumberInput');
            if (!mobileInput || !mobileInput.value) { alert('Enter Mobile'); return; }
            encryptedValue = await encryptWithWebCrypto(mobileInput.value, publicKeyPem);
            formData.append('MobileNumber', encryptedValue);
        }
        else
        {
            const passInput = document.getElementById('passwordInput');
            if (!passInput || !passInput.value) { alert('Enter Password'); return; }
            encryptedValue = await encryptWithWebCrypto(passInput.value, publicKeyPem);
            formData.append('P', encryptedValue);
        }

        if (!encryptedValue) throw new Error('Encryption failed.');

        const response = await fetch('api/auth/login', {
            method: 'POST',
            body: formData
        });

        if (response.redirected || response.url) {
            window.location.href = response.url;
        }

    } catch (error) {
        console.error('Login error:', error);
        alert('An error occurred. Please try again.');
    }
}

async function performOtpVerify(userId) {
    try {
        const otpInput = document.getElementById('otpInput');
        if (!otpInput || !otpInput.value) {
            alert('Please enter OTP');
            return;
        }

        const formData = new FormData();
        formData.append('UserId', userId);
        formData.append('UserOtp', otpInput.value);

        const response = await fetch('api/auth/verify-otp', {
            method: 'POST',
            body: formData
        });

        if (response.redirected || response.url) {
            window.location.href = response.url;
        }
    } catch (error) {
        console.error('OTP error:', error);
    }
}