(function () {
    let totalCartQty = 0;

    totalCartQty = window.initialCartItemCount || 0;

    function updateHeaderCount() {
        const badge = document.getElementById('cartItemBadge');
        if (!badge) return;

        badge.textContent = totalCartQty + (totalCartQty === 1 ? ' item' : ' items');
    }

    function updateNavCartCount() {
        const nav = document.getElementById('navCartCount');
        if (!nav) return;

        nav.textContent = totalCartQty;
    }

    function validateCheckoutButton() {
        const anyChecked = document.querySelectorAll('.item-checkbox:checked').length > 0;
        const btn = document.getElementById('checkoutBtn');

        if (!btn) return;

        if (!anyChecked) {
            btn.classList.add('disabled');
            btn.style.pointerEvents = 'none';
            btn.style.opacity = '0.5';
        } else {
            btn.classList.remove('disabled');
            btn.style.pointerEvents = 'auto';
            btn.style.opacity = '1';
        }
    }

    function updateSelectedItemsInput() {
        const checked = [...document.querySelectorAll('.item-checkbox:checked')]
            .map(cb => cb.value);

        document.getElementById('selectedItemsInput').value = checked.join(',');
    }

    // Helpers
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    async function postForm(url, data) {
        const body = new URLSearchParams(data);

        if (token) {
            body.append('__RequestVerificationToken', token);
        }

        const res = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body
        });

        return res;
    }

    // Summary recalculation
    function recalc() {
        const FREE_SHIP_THRESHOLD = 100;
        const SHIP_COST = 10;

        let totalQty = 0;
        let subtotal = 0;

        document.querySelectorAll('.cart-item').forEach(row => {
            const cb = row.querySelector('.item-checkbox');

            if (!cb || !cb.checked) return;

            const price = parseFloat(row.dataset.price) || 0;
            const qty = parseInt(row.querySelector('.qty-value').value, 10) || 0;

            totalQty += qty;
            subtotal += price * qty;
        });

        const shipping =
            subtotal === 0
                ? 0
                : subtotal >= FREE_SHIP_THRESHOLD
                    ? 0
                    : SHIP_COST;

        const total = subtotal + shipping;

        document.getElementById('summaryItemCount').textContent = totalQty;
        document.getElementById('summaryItemWord').textContent =
            totalQty === 1 ? 'item' : 'items';

        document.getElementById('summarySubtotal').textContent =
            subtotal.toFixed(2);

        document.getElementById('summaryTotal').textContent =
            total.toFixed(2);

        const shippingValueEl = document.getElementById('shippingValue');
        const freeShippingHint = document.getElementById('freeShippingHint');
        const freeShippingText = document.getElementById('freeShippingHintText');

        if (subtotal === 0) {
            shippingValueEl.textContent = 'RM 0.00';
            freeShippingHint.style.display = 'none';
        } else if (shipping === 0) {
            shippingValueEl.textContent = 'Free';
            freeShippingHint.style.display = 'none';
        } else {
            shippingValueEl.textContent = 'RM ' + SHIP_COST.toFixed(2);
            freeShippingHint.style.display = '';

            freeShippingText.textContent =
                'Add RM ' +
                (FREE_SHIP_THRESHOLD - subtotal).toFixed(2) +
                ' more for free shipping';
        }
    }

    // Select all checkbox
    const selectAll = document.getElementById('selectAll');

    if (selectAll) {
        selectAll.addEventListener('change', () => {
            document.querySelectorAll('.item-checkbox').forEach(cb => {
                cb.checked = selectAll.checked;
                validateCheckoutButton();
            });

            updateSelectedItemsInput();
            recalc();
        });
    }

    // Per item checkbox
    document.querySelectorAll('.item-checkbox').forEach(cb => {
        cb.addEventListener('change', () => {
            const allChecked = [...document.querySelectorAll('.item-checkbox')]
                .every(c => c.checked);

            if (selectAll) {
                selectAll.checked = allChecked;
            }

            validateCheckoutButton();
            updateSelectedItemsInput();
            recalc();
        });
    });

    // Quantity buttons
    document.querySelectorAll('.qty-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const row = btn.closest('.cart-item');
            const qtyInput = row.querySelector('.qty-value');
            const itemId = btn.dataset.itemId;

            const isPlus = btn.classList.contains('qty-plus');

            const currentQty =
                parseInt(qtyInput.value, 10) || 1;

            const stock = getRowStock(row);

            if (isPlus && currentQty >= stock) {
                return;
            }

            const newQty = isPlus
                ? currentQty + 1
                : Math.max(1, currentQty - 1);

            const oldQty =
                parseInt(qtyInput.value, 10) || 1;

            if (newQty === currentQty) return;

            totalCartQty += (newQty - oldQty);

            updateHeaderCount();
            updateNavCartCount();

            // Optimistic UI update
            qtyInput.value = newQty;

            // Update subtotal
            const price = parseFloat(row.dataset.price) || 0;

            const subtotal =
                row.querySelector('.item-subtotal-value');

            if (subtotal) {
                subtotal.textContent =
                    (price * newQty).toFixed(2);
            }

            updateQuantityButtonStates(row);

            recalc();

            try {
                await postForm('/Customer/UpdateQuantity', {
                    cartItemId: itemId,
                    quantity: newQty
                });
            } catch (e) {
                console.error('Failed to update quantity', e);
            }
        });
    });

    // Helper function to update button states based on stock
    function updateQuantityButtonStates(row) {
        const qtyInput = row.querySelector('.qty-value');
        const plusBtn = row.querySelector('.qty-plus');
        const minusBtn = row.querySelector('.qty-minus');
        const currentQty = parseInt(qtyInput.value, 10) || 1;
        const stock = getRowStock(row);

        if (plusBtn) {
            if (currentQty >= stock) {
                plusBtn.classList.add('disabled');
                plusBtn.style.pointerEvents = 'none';
                plusBtn.style.opacity = '0.5';
                plusBtn.style.cursor = 'not-allowed';
            } else {
                plusBtn.classList.remove('disabled');
                plusBtn.style.pointerEvents = 'auto';
                plusBtn.style.opacity = '1';
                plusBtn.style.cursor = 'pointer';
            }
        }

        if (minusBtn) {
            if (currentQty <= 1) {
                minusBtn.classList.add('disabled');
                minusBtn.style.pointerEvents = 'none';
                minusBtn.style.opacity = '0.5';
                minusBtn.style.cursor = 'not-allowed';
            } else {
                minusBtn.classList.remove('disabled');
                minusBtn.style.pointerEvents = 'auto';
                minusBtn.style.opacity = '1';
                minusBtn.style.cursor = 'pointer';
            }
        }
    }

    // Helper to read/compute stock for a cart row (prefers variation combos + selected variations)
    function getRowStock(row) {
        let stock = parseInt(row.dataset.stock, 10) || 0;

        try {
            const combosJson = row.dataset.variationCombos || row.getAttribute('data-variation-combos');
            const selectedJson = row.dataset.selectedVariations || row.getAttribute('data-selected-variations');

            if (combosJson && combosJson !== '[]' && selectedJson && selectedJson !== '{}' ) {
                const selectedObj = JSON.parse(selectedJson);
                const selectedValues = Object.values(selectedObj || {})
                    .filter(v => v != null)
                    .map(v => String(v).trim().toLowerCase());

                const combos = JSON.parse(combosJson);
                if (Array.isArray(combos)) {
                    for (const c of combos) {
                        const keys = Array.isArray(c.keys) ? c.keys : [];
                        if (keys.length > 0) {
                            const allMatch = keys.every(k => selectedValues.includes(String(k || '').trim().toLowerCase()));
                            if (allMatch) {
                                const s = c.stock;
                                const parsed = parseInt(s, 10);
                                if (!isNaN(parsed)) stock = parsed;
                                break;
                            }
                        }
                    }
                }
            }
        } catch (e) {
            // ignore and fallback to server-provided stock
        }

        return stock;
    }

    // Initialize button states on page load
    document.querySelectorAll('.cart-item').forEach(row => {
        updateQuantityButtonStates(row);
    });

    // Remove item
    document.querySelectorAll('.btn-remove-ajax').forEach(btn => {
        btn.addEventListener('click', async () => {
            const row = btn.closest('.cart-item');
            const itemId = btn.dataset.itemId;

            const qty =
                parseInt(row.querySelector('.qty-value').value, 10) || 1;

            totalCartQty -= qty;

            updateHeaderCount();
            updateNavCartCount();

            // Animate out
            row.style.transition = 'opacity 0.25s, transform 0.25s';
            row.style.opacity = '0';
            row.style.transform = 'translateX(20px)';

            try {
                await postForm('/Customer/RemoveItem', {
                    cartItemId: itemId
                });
            } catch (e) {
                console.error('Failed to remove item', e);

                row.style.opacity = '1';
                row.style.transform = '';

                return;
            }

            setTimeout(() => {
                row.remove();

                recalc();

                if (!document.querySelector('.cart-item')) {
                    location.reload();
                }
            }, 260);
        });
    });

    // Initial load
    recalc();
    validateCheckoutButton();
})();