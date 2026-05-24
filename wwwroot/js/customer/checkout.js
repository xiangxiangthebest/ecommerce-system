(function () {

    const source = window.checkoutSource;
    const productId = window.checkoutProductId;

    const backBtn = document.getElementById("backBtn");

    if (backBtn) {
        if (source === "product") {
            backBtn.href = `/Customer/ProductDetails/${productId}`;
        } else {
            backBtn.href = `/Customer/Cart`;
        }
    }

    document.addEventListener("DOMContentLoaded", function () {

        // Address select UI highlight
        const radios = document.querySelectorAll('input[name="SelectedAddressId"]');

        radios.forEach(radio => {
            radio.addEventListener("change", function () {

                document.querySelectorAll(".saved-address-card")
                    .forEach(card => card.classList.remove("selected"));

                this.closest(".saved-address-card")
                    ?.classList.add("selected");

                updatePlaceOrderState();
            });
        });

        updatePlaceOrderState();
    });

    /* Payment method */
    const pmRadios = document.querySelectorAll('.pm-radio');
    const cardForm = document.getElementById('cardDetailsForm');

    function refreshPayment() {

        const val = document.querySelector('.pm-radio:checked')?.value;

        document.querySelectorAll('.payment-option').forEach(el => {
            el.classList.toggle(
                'selected',
                el.querySelector('.pm-radio')?.checked
            );
        });

        if (cardForm) {
            cardForm.style.display = val === 'Card' ? '' : 'none';
        }

        updatePlaceOrderState();
    }

    pmRadios.forEach(r => r.addEventListener('change', refreshPayment));
    refreshPayment();

    /* Card formatting */
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

            if (v.length >= 3) {
                v = v.substring(0, 2) + ' / ' + v.substring(2);
            }

            this.value = v;
        });
    }

    /* Promo codes */
    const PROMO_CODES = {
        'SAVE10':   { type: 'voucher', amount: 10, label: 'RM10 off' },
        'FREESHIP': { type: 'ship', amount: null, label: 'Free shipping' },
        'WELCOME5': { type: 'voucher', amount: 5, label: 'RM5 off' }
    };

    let appliedPromo = null;

    const applyBtn = document.getElementById('applyPromoBtn');
    const promoFeedback = document.getElementById('promoFeedback');

    function recalcSummary() {

        const base =
            parseFloat(document.getElementById('sumMerchandise').textContent) || 0;

        let shipBase = base >= 100 ? 0 : 10;
        let shipSst  = +(shipBase * 0.08).toFixed(2);

        let shipDisc = 0;
        let voucherD = 0;

        if (appliedPromo) {
            if (appliedPromo.type === 'ship') {
                shipDisc = +(shipBase + shipSst).toFixed(2);
                shipBase = 0;
                shipSst = 0;
            } else {
                voucherD = appliedPromo.amount;
            }
        }

        const total =
            Math.max(0, base + shipBase + shipSst - shipDisc - voucherD);

        document.getElementById('sumShippingBase').textContent =
            shipBase.toFixed(2);

        document.getElementById('sumShippingSst').textContent =
            shipSst.toFixed(2);

        document.getElementById('lineShipDiscount').style.display =
            shipDisc > 0 ? '' : 'none';

        document.getElementById('lineVoucherDiscount').style.display =
            voucherD > 0 ? '' : 'none';

        document.getElementById('sumShipDiscount').textContent =
            shipDisc.toFixed(2);

        document.getElementById('sumVoucherDiscount').textContent =
            voucherD.toFixed(2);

        document.getElementById('sumTotal').textContent =
            total.toFixed(2);

        document.getElementById('btnTotal').textContent =
            total.toFixed(2);
    }

    if (applyBtn) {
        applyBtn.addEventListener('click', function () {

            const input = document.getElementById('promoInput');
            const code = (input?.value || '').trim().toUpperCase();
            const promo = PROMO_CODES[code];

            if (!code) {
                promoFeedback.textContent = '';
                return;
            }

            if (promo) {
                appliedPromo = promo;

                promoFeedback.textContent =
                    `✓ "${code}" applied — ${promo.label}`;

                promoFeedback.className = 'promo-feedback success';

                applyBtn.textContent = 'Remove';

                applyBtn.onclick = function () {
                    appliedPromo = null;
                    promoFeedback.textContent = '';
                    promoFeedback.className = 'promo-feedback';

                    if (input) input.value = '';

                    applyBtn.textContent = 'Apply';

                    location.reload();
                };

            } else {
                appliedPromo = null;

                promoFeedback.textContent =
                    '✕ Invalid promo code. Try SAVE10, FREESHIP or WELCOME5';

                promoFeedback.className = 'promo-feedback error';
            }

            recalcSummary();
        });
    }

    /* Place order validation */
    function updatePlaceOrderState() {

        const btn = document.getElementById('placeOrderBtn');
        const msg = document.getElementById('checkoutGuardMsg');

        if (!btn) return;

        const addressChecked =
            document.querySelector('input[name="SelectedAddressId"]:checked');

        const paymentChecked =
            document.querySelector('input[name="PaymentMethod"]:checked');

        const ok = !!addressChecked && !!paymentChecked;

        btn.disabled = !ok;

        if (msg) {
            if (ok) {
                msg.textContent = '';
                msg.className = 'checkout-guard-msg';
            } else {
                if (!addressChecked) {
                    msg.textContent =
                        'Please select a delivery address to continue.';
                } else if (!paymentChecked) {
                    msg.textContent =
                        'Please select a payment method to continue.';
                }

                msg.className = 'checkout-guard-msg show';
            }
        }
    }

    document.getElementById('checkoutForm')
        ?.addEventListener('submit', function (e) {

            updatePlaceOrderState();

            if (document.getElementById('placeOrderBtn')?.disabled) {
                e.preventDefault();

                const hasAddress =
                    !!document.querySelector(
                        'input[name="SelectedAddressId"]:checked'
                    );

                const pmChosen =
                    !!document.querySelector('.pm-radio:checked');

                if (!hasAddress) {
                    document.querySelector('.checkout-card')
                        ?.scrollIntoView({ behavior: 'smooth' });
                } else if (!pmChosen) {
                    document.querySelector('.payment-methods')
                        ?.scrollIntoView({ behavior: 'smooth' });
                }
            }
        });

}());