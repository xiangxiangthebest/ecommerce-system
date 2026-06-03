function togglePassword(inputId, el) {
        const input   = document.getElementById(inputId);
        const eyeOn   = el.querySelector('.eye-icon');
        const eyeOff  = el.querySelector('.eye-off-icon');

        if (input.type === 'password') {
            input.type = 'text';
            eyeOn.style.display = 'none';
            eyeOff.style.display = 'block';
        } else {
            input.type = 'password';
            eyeOn.style.display = 'block';
            eyeOff.style.display = 'none';
        }
    }