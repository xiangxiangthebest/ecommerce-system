(function () {
    const actionForms = document.querySelectorAll('.action-form');

    actionForms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const isReject = form.querySelector('.reject-btn');
            const isApprove = form.querySelector('.approve-btn');

            if (isApprove) {
                const requestId = form.querySelector('input[name="requestId"]').value;
                const checkboxes = document.querySelectorAll(
                    `.ri-check[form="approve-form-${requestId}"]`
                );

                const approveItems = [];
                checkboxes.forEach(cb => {
                    if (cb.checked) {
                        const qtyInput = document.querySelector(
                            `.ri-qty-input[form="approve-form-${requestId}"]`
                        );
                        const orderItemId = parseInt(cb.dataset.orderItemId);
                        const qty = parseInt(cb.parentElement.querySelector('.ri-qty-input').value) || 0;

                        if (qty > 0) {
                            approveItems.push({ orderItemId: orderItemId, qty: qty });
                        }
                    }
                });

                if (approveItems.length === 0) {
                    alert('Please select at least one item to approve, or click Reject instead.');
                    e.preventDefault();
                    return;
                }

                form.querySelector('.approve-items-json').value = JSON.stringify(approveItems);

                // if (!confirm(`Approve ${approveItems.length} item(s)?`)) {
                //     e.preventDefault();
                // }
                return;
            }

            if (isReject) {
                if (!confirm('Reject this request?')) {
                    e.preventDefault();
                }
            }
        });
    });
})();

