(function () {
    const actionForms = document.querySelectorAll('.action-form');

    actionForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const isReject = form.querySelector('.reject-btn');
            const message = isReject
                ? 'Reject this request?'
                : 'Approve this request?';

            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });
})();