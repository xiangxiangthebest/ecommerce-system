document.addEventListener('DOMContentLoaded', function () {

    /* 1 ─ Auto-scroll to bottom ─────────────────────── */
    const chatBody = document.getElementById('chatBody');
    if (chatBody) chatBody.scrollTop = chatBody.scrollHeight;

    const cfg      = window.ChatConfig || {};
    const sellerId = cfg.sellerId  || 0;
    const isSeller = cfg.isSeller  || false;

    /* 2 ─ Legacy garbled-title cleanup ──────────────── */
    document.querySelectorAll('.msg-product-card').forEach(function (bubble) {
        const titleEl = bubble.querySelector('.msg-card-title');
        if (!titleEl) return;
        let txt = titleEl.innerText.trim();
        if (txt.includes('search=') || txt.includes('%') || txt.includes('Home?')) {
            try {
                while (txt.includes('%')) {
                    const old = txt;
                    txt = decodeURIComponent(txt);
                    if (old === txt) break;
                }
                if (txt.includes('search=')) txt = txt.split('search=')[1];
                const pts = txt.split('/');
                let final = pts[pts.length - 1] || 'Item';
                if (final.includes('?')) final = final.split('?')[0];
                titleEl.innerText = final.trim();
            } catch (e) { console.warn('Title cleanup:', e); }
        }
    });

    /* 3 ─ Attach panel toggle ────────────────────────── */
    const attachToggleBtn = document.getElementById('attachToggleBtn');
    const attachPanel     = document.getElementById('attachPanel');
    if (attachToggleBtn && attachPanel) {
        attachToggleBtn.addEventListener('click', function () {
            attachPanel.classList.toggle('open');
        });
    }

    /* 4 ─ Seller products modal ─────────────────────── */
    const sellerProductsModal = document.getElementById('sellerProductsModal');
    if (sellerProductsModal) {
        sellerProductsModal.addEventListener('show.bs.modal', function () {
            const container = document.getElementById('sellerProductsContainer');
            container.innerHTML = '<div class="modal-state"><i class="ti ti-loader"></i>Loading products…</div>';

            fetch('/Chat/GetSellerProducts?sellerId=' + sellerId)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data.length) {
                        container.innerHTML = '<div class="modal-state"><i class="ti ti-shopping-bag"></i>No products found.</div>';
                        return;
                    }
                    container.innerHTML = data.map(function (p) {
                        var safe = p.name.replace(/'/g, "\\'").replace(/"/g, '\\"');
                        return '<div class="modal-list-item" onclick="sendCustomCard(\'PRODUCT\',\'' +
                            encodeURIComponent(safe) + '\',\'' + p.productId + '\',\'' +
                            p.price + '\',\'' + p.imagePath + '\')">' +
                            '<img src="' + p.imagePath + '" class="modal-list-thumb" onerror="this.src=\'/images/default-product.jpg\'" />' +
                            '<div class="modal-list-info">' +
                            '<p class="modal-list-name">' + p.name + '</p>' +
                            '<p class="modal-list-price">RM ' + p.price + '</p>' +
                            '</div>' +
                            '<button class="modal-send-btn" type="button"><i class="ti ti-send"></i> Send</button>' +
                            '</div>';
                    }).join('');
                })
                .catch(function () {
                    container.innerHTML = '<div class="modal-state error"><i class="ti ti-alert-circle"></i>Failed to load. Please try again.</div>';
                });
        });
    }

    /* 5 ─ Customer orders modal ──────────────────────── */
    const customerOrdersModal = document.getElementById('customerOrdersModal');
    if (customerOrdersModal) {
        customerOrdersModal.addEventListener('show.bs.modal', function () {
            const container = document.getElementById('customerOrdersContainer');
            container.innerHTML = '<div class="modal-state"><i class="ti ti-loader"></i>Loading orders…</div>';

            fetch('/Chat/GetCustomerOrders?sellerId=' + sellerId)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data.length) {
                        container.innerHTML = '<div class="modal-state"><i class="ti ti-receipt"></i>No orders with this seller yet.</div>';
                        return;
                    }
                    container.innerHTML = data.map(function (o) {
                        var safe = (o.coverName || 'Ordered Item').replace(/'/g, "\\'").replace(/"/g, '\\"');
                        var img  = o.coverImage || '/images/default-order.png';
                        return '<div class="modal-list-item">' +
                            '<img src="' + img + '" class="modal-list-thumb" onerror="this.src=\'/images/default-order.png\'" />' +
                            '<div class="modal-list-info">' +
                            '<p class="modal-list-name">Order #' + o.orderId + '</p>' +
                            '<p class="modal-list-sub">' + (o.coverName || 'Ordered Item') + '</p>' +
                            '<p class="modal-list-price">RM ' + o.totalAmount + '</p>' +
                            '</div>' +
                            '<button class="modal-send-btn" type="button" ' +
                            'onclick="sendCustomCard(\'ORDER\',\'' + encodeURIComponent(safe) + '\',\'' +
                            o.orderId + '\',\'' + o.totalAmount + '\',\'' + img + '\')">' +
                            '<i class="ti ti-send"></i> Send</button>' +
                            '</div>';
                    }).join('');
                })
                .catch(function () {
                    container.innerHTML = '<div class="modal-state error"><i class="ti ti-alert-circle"></i>Failed to load. Please try again.</div>';
                });
        });
    }

    /* 6 ─ Product preview bar "Send" ────────────────── */
    const sendProductBtn = document.getElementById('sendProductLinkBtn');
    if (sendProductBtn) {
        sendProductBtn.addEventListener('click', function () {
            var c = window.ChatConfig || {};
            sendCustomCard(
                'PRODUCT',
                encodeURIComponent(c.chatProductName  || ''),
                c.chatProductId    || '',
                c.chatProductPrice || '',
                c.chatProductImage || ''
            );
        });
    }

    /* 7 ─ Photo attachment ───────────────────────────── */
    const attachPhotoBtn = document.getElementById('attachPhotoBtn');
    const photoFileInput = document.getElementById('photoFileInput');
    if (attachPhotoBtn && photoFileInput) {
        attachPhotoBtn.addEventListener('click', function () { photoFileInput.click(); });
        photoFileInput.addEventListener('change', function () {
            var file = this.files[0];
            if (!file) return;
            if (!file.type.startsWith('image/')) { alert('Please select a valid image file.'); return; }
            if (file.size > 5 * 1024 * 1024)    { alert('Image must be under 5 MB.'); return; }

            var reader = new FileReader();
            reader.onload = function (e) {
                var input = document.getElementById('messageInput');
                var form  = document.getElementById('chatForm');
                if (input && form) {
                    input.value = '[IMAGE_CARD]<div class="msg-image-bubble">' +
                        '<img src="' + e.target.result + '" alt="image" /></div>';
                    input.removeAttribute('required');
                    form.submit();
                }
            };
            reader.readAsDataURL(file);
        });
    }

    /* 8 ─ Seller-side card click interception ────────── */
    if (isSeller) {
        document.addEventListener('click', function (e) {

            /* Product card → Seller product search */
            var productBubble = e.target.closest('.msg-product-card');
            if (productBubble && !e.target.closest('.msg-order-card')) {
                var anchor = productBubble.querySelector('a');
                if (anchor) {
                    e.preventDefault();
                    var titleEl = productBubble.querySelector('.msg-card-title');
                    var keyword = titleEl ? titleEl.innerText.trim() : '';
                    if (!keyword || keyword.includes('%') || keyword.includes('Home?')) {
                        var href = anchor.getAttribute('href') || '';
                        try { while (href.includes('%')) href = decodeURIComponent(href); } catch (_) {}
                        if (href.includes('search=')) href = href.split('search=')[1];
                        var pts = href.split('/');
                        keyword = pts[pts.length - 1] || '';
                        if (keyword.includes('?')) keyword = keyword.split('?')[0];
                    }
                    if (keyword) {
                        window.location.href = window.location.origin + '/Seller/Home?tab=Product&search=' + encodeURIComponent(keyword.trim());
                    }
                }
            }

            /* Order card → Seller order search */
            var orderBubble = e.target.closest('.msg-order-card');
            if (orderBubble) {
                var anchor2 = orderBubble.querySelector('a');
                if (anchor2) {
                    e.preventDefault();
                    var orderId = (orderBubble.getAttribute('data-order-id') || '0').trim();
                    if (orderId === '0') {
                        var parts = (anchor2.getAttribute('href') || '').split('=');
                        orderId = parts[parts.length - 1] || '0';
                    }
                    if (orderId && orderId !== '0') {
                        window.location.href = window.location.origin + '/Seller/Home?tab=Order&search=' + encodeURIComponent(orderId);
                    }
                }
            }
        });
    }

}); /* end DOMContentLoaded */


/* ══════════════════════════════════════════════════════
   Global helpers – called from Razor inline or modals
   ══════════════════════════════════════════════════════ */

/**
 * Build and submit a rich card message.
 * @param {'PRODUCT'|'ORDER'} type
 * @param {string} title   URL-encoded name
 * @param {string|number} id
 * @param {string|number} price
 * @param {string} img
 */
function sendCustomCard(type, title, id, price, img) {
    var input = document.getElementById('messageInput');
    var form  = document.getElementById('chatForm');
    if (!input || !form) return;

    var cleanTitle = title;
    try { cleanTitle = decodeURIComponent(title); } catch (_) {}
    cleanTitle = cleanTitle.replace(/'/g, '').replace(/"/g, '').trim();

    if (type === 'PRODUCT') {
        var url = '/Customer/ProductDetails/' + id;
        input.value =
            '[PRODUCT_CARD]' +
            '<a href="' + url + '" class="msg-card-link">' +
            '<div class="msg-card-chip">' +
            '<span class="chip-label"><i class="ti ti-shopping-bag" style="font-size:12px"></i> Product</span>' +
            '<span class="chip-action">View details →</span>' +
            '</div>' +
            '<div class="msg-card-body">' +
            '<img src="' + img + '" class="msg-card-thumb" onerror="this.src=\'/images/default-product.jpg\'" />' +
            '<div class="msg-card-info">' +
            '<p class="msg-card-title js-product-title">' + cleanTitle + '</p>' +
            '<p class="msg-card-sub">Tap to view product page</p>' +
            '</div>' +
            '</div>' +
            '<div class="msg-card-footer">' +
            '<span class="msg-card-price">RM ' + price + '</span>' +
            '<span class="msg-card-view"><i class="ti ti-chevron-right" style="font-size:13px"></i></span>' +
            '</div>' +
            '</a>';
    } else if (type === 'ORDER') {
        input.value = '[ORDER_TAG]|' + id + '|' + price + '|' + img + '|' + cleanTitle;
    }

    input.removeAttribute('required');

    /* Close open modals */
    document.querySelectorAll('.modal.show').forEach(function (m) {
        var inst = bootstrap.Modal.getInstance(m);
        if (inst) inst.hide();
    });

    /* Close attach panel */
    var panel = document.getElementById('attachPanel');
    if (panel) panel.classList.remove('open');

    form.submit();
}

/**
 * Full-screen image preview.
 * @param {HTMLElement} element  the .msg-image-bubble wrapper
 */
function previewChatImage(element) {
    var img = element.querySelector('img');
    if (!img) return;
    var preview = document.getElementById('previewModalImage');
    if (preview) preview.src = img.getAttribute('src');
    var modalEl = document.getElementById('imagePreviewModal');
    if (modalEl) new bootstrap.Modal(modalEl).show();
}
