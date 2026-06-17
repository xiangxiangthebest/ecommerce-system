(function () {

    /* ───────────────────── GLOBALS ───────────────────── */
    const source = window.checkoutSource;
    const productId = window.checkoutProductId;
    const availableVouchers = window.availableVouchers || [];

    let appliedVoucher = null;

    /* ───────────────────── INIT ───────────────────── */
    document.addEventListener("DOMContentLoaded", init);

    function init() {
        setupBackButton();
        setupAddressSelection();
        setupPayment();
        setupCardFormatting();
        setupVoucherUI();
        refreshPayment();
        updateVoucherStubStates();
        updatePlaceOrderState();
    }

    /* ───────────────────── BACK BUTTON ───────────────────── */
    function setupBackButton() {
        const backBtn = document.getElementById("backBtn");
        if (!backBtn) return;

        backBtn.href = source === "product"
            ? `/Customer/ProductDetails/${productId}`
            : `/Customer/Cart`;
    }

    /* ───────────────────── ADDRESS SELECTION ───────────────────── */
    function setupAddressSelection() {
        document.querySelectorAll('input[name="SelectedAddressId"]')
            .forEach(radio => {
                radio.addEventListener("change", () => {
                    document.querySelectorAll(".saved-address-card")
                        .forEach(card => card.classList.remove("selected"));

                    radio.closest(".saved-address-card")?.classList.add("selected");

                    updatePlaceOrderState();
                });
            });
    }

    /* ───────────────────── PAYMENT ───────────────────── */
    function setupPayment() {

        document.querySelectorAll('.pm-radio')
            .forEach(r => r.addEventListener('change', refreshPayment));
    }

    function refreshPayment() {
        const val = document.querySelector('.pm-radio:checked')?.value;

        document.querySelectorAll('.payment-option').forEach(el => {
            el.classList.toggle(
                'selected',
                el.querySelector('.pm-radio')?.checked
            );
        });

        const cardForm = document.getElementById('cardDetailsForm');
        if (cardForm) {
            cardForm.style.display = val === 'Card' ? '' : 'none';
        }

        updateVoucherStubStates();
        updatePlaceOrderState();
    }

    /* ───────────────────── CARD INPUT FORMAT ───────────────────── */
    function setupCardFormatting() {

        const cardNumInput = document.getElementById('cardNumberInput');
        if (cardNumInput) {
            cardNumInput.addEventListener('input', function () {
                let v = this.value.replace(/\D/g, '').substring(0, 16);
                this.value = v.replace(/(.{4})/g, '$1 ').trim();
            });
        }

        const cardExpiry = document.getElementById('cardExpiry');
        if (cardExpiry) {
            cardExpiry.addEventListener('input', function () {
                let v = this.value.replace(/\D/g, '').substring(0, 4);
                this.value = v.length >= 3
                    ? v.substring(0, 2) + ' / ' + v.substring(2)
                    : v;
            });
        }
    }

    /* ───────────────────── VOUCHER UI ───────────────────── */
    function setupVoucherUI() {

        const browseBtn   = document.getElementById('voucherBrowseBtn');
        const voucherTray = document.getElementById('voucherTray');

        /* Toggle tray */
        browseBtn?.addEventListener('click', () => {
            const open = voucherTray.classList.toggle('open');
            browseBtn.classList.toggle('open', open);
        });

        /* Voucher click (auto apply) */
        document.querySelectorAll('.voucher-stub').forEach(stub => {
            stub.addEventListener('click', () => {
                const isDisabled = stub.dataset.disabled === 'true';
                if (isDisabled) {
                    const min = Number(stub.dataset.min || 0);
                    showPromoError(`This voucher requires a minimum spend of RM ${min.toFixed(2)}.`);
                    return;
                }

                applyVoucher(stub.dataset.code, true);

                document.querySelectorAll('.voucher-stub')
                    .forEach(s => s.classList.remove('stub-selected'));
                stub.classList.add('stub-selected');

                voucherTray?.classList.remove('open');
                browseBtn?.classList.remove('open');
            });
        });

        document.getElementById('applyPromoBtn')
            ?.addEventListener('click', () => {
                if (appliedVoucher) {
                    removeVoucher();
                    return;
                }
                const code = document.getElementById('promoInput').value;
                applyVoucher(code);
            });

        document.getElementById('ticketRemoveBtn')
            ?.addEventListener('click', removeVoucher);
    }

    function getCheckoutSubtotal() {
        return parseFloat(document.getElementById('sumMerchandise').textContent) || 0;
    }

    function updateVoucherStubStates() {
        const subtotal = getCheckoutSubtotal();

        document.querySelectorAll('.voucher-stub').forEach(stub => {
            const min = Number(stub.dataset.min || 0);
            const disabled = min > 0 && subtotal < min;

            stub.disabled = disabled;
            stub.classList.toggle('disabled', disabled);
            stub.dataset.disabled = disabled ? 'true' : 'false';
        });
    }

    function showPromoError(message) {
        const feedback = document.getElementById('promoFeedback');
        if (!feedback) return;
        feedback.textContent = message;
        feedback.className = 'promo-feedback error';
    }

    /* ───────────────────── VOUCHER LOGIC ───────────────────── */
    function applyVoucher(code, silent = false) {

        const input       = document.getElementById('promoInput');
        const feedback    = document.getElementById('promoFeedback');
        const selectedInput = document.getElementById('SelectedVoucherId');
        const applyBtn    = document.getElementById('applyPromoBtn');

        code = (code || '').trim().toUpperCase();

        if (appliedVoucher) {
            removeVoucher();
        }

        if (!code) {
            showError('Enter a voucher code.');
            return;
        }

        const voucher = availableVouchers.find(v =>
            (v.Code || '').toUpperCase() === code
        );

        if (!voucher) {
            showError('✕ Invalid voucher code.');
            return;
        }

        const minSpend = Number(voucher.MinimumSpend || 0);
        const subtotal = getCheckoutSubtotal();
        if (minSpend > 0 && subtotal < minSpend) {
            showError(`This voucher requires a minimum spend of RM ${minSpend.toFixed(2)}.`);
            return;
        }

        appliedVoucher = voucher;
        if (selectedInput) selectedInput.value = voucher.Id;
        if (input) input.value = code;

        applyBtn.textContent = 'Remove';

        const savings = voucher.IsPercentage
            ? subtotal * Number(voucher.DiscountValue) / 100
            : Number(voucher.DiscountValue);

        showAppliedTicket(code, savings);
        recalcSummary();
        updatePlaceOrderState();

        function showError(msg) {
            if (silent) return;
            feedback.textContent = msg;
            feedback.className = 'promo-feedback error';
        }
    }

    function removeVoucher() {

        appliedVoucher = null;

        document.getElementById('SelectedVoucherId').value = '';
        document.getElementById('promoInput').value = '';
        document.getElementById('applyPromoBtn').textContent = 'Apply';

        document.getElementById('voucherAppliedTicket').style.display = 'none';
        document.querySelectorAll('.voucher-stub')
            .forEach(s => s.classList.remove('stub-selected'));

        recalcSummary();
    }

    function showAppliedTicket(code, savings) {
        document.getElementById('voucherAppliedTicket').style.display = 'flex';
        document.getElementById('ticketCode').textContent = code;
        document.getElementById('ticketSavings').textContent = savings.toFixed(2);

        const fb = document.getElementById('promoFeedback');
        fb.textContent = '';
        fb.className = 'promo-feedback';
    }

    /* ───────────────────── CALCULATION ───────────────────── */
    function recalcSummary() {

        const base =
            parseFloat(document.getElementById('sumMerchandise').textContent) || 0;

        let shipBase = base >= 100 ? 0 : 10;
        let shipSst  = +(shipBase * 0.08).toFixed(2);

        let voucherD = appliedVoucher
            ? Number(appliedVoucher.DiscountValue)
            : 0;

        const total = Math.max(0, base + shipBase + shipSst - voucherD);

        setText('sumShippingBase', shipBase);
        setText('sumShippingSst', shipSst);
        setText('sumVoucherDiscount', voucherD);
        setText('sumTotal', total);
        setText('btnTotal', total);

        toggle('lineVoucherDiscount', voucherD > 0);
    }

    function setText(id, val) {
        document.getElementById(id).textContent = val.toFixed(2);
    }

    function toggle(id, show) {
        document.getElementById(id).style.display = show ? '' : 'none';
    }

    /* ───────────────────── PLACE ORDER VALIDATION ───────────────────── */
    function updatePlaceOrderState() {

        const btn = document.getElementById('placeOrderBtn');
        const msg = document.getElementById('checkoutGuardMsg');

        if (!btn) return;

        const hasAddress = !!document.querySelector('[name="SelectedAddressId"]:checked');
        const hasPayment = !!document.querySelector('[name="PaymentMethod"]:checked');

        const ok = hasAddress && hasPayment;

        btn.disabled = !ok;

        if (!msg) return;

        if (ok) {
            msg.textContent = '';
            msg.className = 'checkout-guard-msg';
        } else {
            msg.textContent = !hasAddress
                ? 'Please select a delivery address.'
                : 'Please select a payment method.';
            msg.className = 'checkout-guard-msg show';
        }
    }

    document.getElementById('checkoutForm')
        ?.addEventListener('submit', function (e) {

            updatePlaceOrderState();

            if (document.getElementById('placeOrderBtn').disabled) {
                e.preventDefault();

                const hasAddress = document.querySelector('[name="SelectedAddressId"]:checked');
                const hasPayment = document.querySelector('.pm-radio:checked');

                (!hasAddress
                    ? document.querySelector('.checkout-card')
                    : document.querySelector('.payment-methods')
                )?.scrollIntoView({ behavior: 'smooth' });
            }
        });

})();