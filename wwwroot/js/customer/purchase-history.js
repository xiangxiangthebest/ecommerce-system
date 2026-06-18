'use strict';

const _orders = window.PH_ORDERS || [];

// ── State ──────────────────────────────────────────────────
let _cancelSelectedReason   = '';
let _rtnFiles               = [];
let _rtnOrderItems          = [];
let _rtnSelectedType        = '';
let _rtnSelectedReason      = '';
let _ratingFilesByOrderItem = {};
let currentReturnOrderId = null;

/* ============================================================
   INIT
   ============================================================ */
document.addEventListener('DOMContentLoaded', () => {
const searchInput = document.getElementById("phSearchInput");
    const tabs = document.querySelectorAll(".ph-tab");
    const cards = document.querySelectorAll(".ph-card");
    const noResults = document.getElementById("phNoResults");

    let currentStatus = "ALL";

    function filterOrders() {
        const query = searchInput ? searchInput.value.toLowerCase().trim() : "";
        let hasVisible = false;

        cards.forEach(card => {
            const searchData = card.getAttribute("data-search") || "";
            const cardStatus = card.getAttribute("data-status") || "";

            const AFTER_SALE_STATUSES = new Set([
                'RETURN_REFUND_REQUESTED', 'RETURN_REFUND', 'REFUND', 'RETURN_REFUND_REJECTED'
            ]);
            const matchesStatus = (
                currentStatus === "ALL" ||
                (currentStatus === "AFTER SALE" && AFTER_SALE_STATUSES.has(cardStatus)) ||
                cardStatus === currentStatus
            );
            const matchesSearch = searchData.includes(query);

            if (matchesStatus && matchesSearch) {
                card.style.display = "";
                hasVisible = true;
            } else {
                card.style.display = "none";
            }
        });

        if (noResults) {
            noResults.style.display = hasVisible ? 'none' : 'flex';
        }
    }

    if (searchInput) {
        searchInput.addEventListener("input", filterOrders);
    }

    tabs.forEach(tab => {
        tab.addEventListener("click", function () {
            tabs.forEach(t => t.classList.remove("active"));
            this.classList.add("active");

            currentStatus = this.getAttribute("data-status");
            filterOrders();
        });
    });

    if (searchInput && searchInput.value.trim() !== "") {
        filterOrders();
    }

    initToast();
    initTabs();
    initSearch();
    initKeyboardClose();
});

function initToast() {
    const toast = document.getElementById('phToast');
    if (toast) setTimeout(() => toast.classList.add('ph-toast-hide'), 3500);
}

function initTabs() {
    const tabs = document.querySelectorAll('#phTabs .ph-tab');
    tabs.forEach(tab => {
        tab.addEventListener('click', function() {
            tabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');
            currentTab = this.getAttribute('data-status');
            applyFilters();
        });
    });

    const searchInput = document.getElementById('phSearchInput');
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            currentSearch = e.target.value.toLowerCase().trim();
            applyFilters();
        });

        if (searchInput.value) {
            currentSearch = searchInput.value.toLowerCase().trim();
            currentTab = 'ALL';
            
            tabs.forEach(t => t.classList.remove('active'));
            const allTabBtn = document.querySelector('#phTabs .ph-tab[data-status="ALL"]');
            if (allTabBtn) allTabBtn.classList.add('active');
            
            applyFilters();
        }
    }
}

function initSearch() {
    document.getElementById('phSearch')?.addEventListener('input', applyFilters);
}

function initKeyboardClose() {
    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        closeDrawer();
        closeCancelModal();
        closeReturnModal();
        closeRatingModal();
        rtnCloseLightbox();
    });
}

/* ============================================================
   FILTERS  (tab + search)
   ============================================================ */
function applyFilters() {
    const cards = document.querySelectorAll('#phOrderList .ph-card');
    let visibleCount = 0;

    cards.forEach(card => {
        const cardStatus = card.getAttribute('data-status');
        const cardSearchInfo = card.getAttribute('data-search') || '';

        const matchesTab = (currentTab === 'ALL' || cardStatus === currentTab);
        
        const matchesSearch = (!currentSearch || cardSearchInfo.includes(currentSearch));

        if (matchesTab && matchesSearch) {
            card.style.display = '';
            visibleCount++;
        } else {
            card.style.display = 'none';
        }
    });

    const noResults = document.getElementById('phNoResults');
    if (noResults) {
        noResults.style.display = visibleCount === 0 ? 'flex' : 'none';
    }
}

function clearSearch() {
    const input = document.getElementById('phSearchInput');
    if (input) {
        input.value = '';
        currentSearch = '';
        applyFilters();
        input.focus();
    }
}

/* ============================================================
   COLLAPSIBLE SECTIONS
   ============================================================ */
function toggleCollapse(btn) {
    btn.classList.toggle('open');
    const collapse = btn.closest('.ph-card-seller, .ph-card-delivery-toggle')
                        .nextElementSibling;
    collapse.style.maxHeight = btn.classList.contains('open')
        ? collapse.scrollHeight + 'px'
        : '0';
}

/* ============================================================
   ORDER DETAIL DRAWER
   ============================================================ */
function openDrawer(orderId) {
    const order = _orders.find(o => o.orderId === orderId);
    if (!order) return;

    document.getElementById('odTitle').textContent = `Order #${order.orderId}`;
    document.getElementById('odDate').textContent  = order.orderTime;

    const badge = document.getElementById('odBadge');
    const isReturnStatus = order.status === 'RETURN_REFUND'
                        || order.status === 'RETURN_REFUND_REQUESTED'
                        || order.status === 'REFUND'
                        || order.status === 'RETURN_REFUND_REJECTED';
    const isApproved     = (order.status === 'RETURN_REFUND' || order.status === 'REFUND') && order.returnApproved;
    const isRejected     = order.status === 'RETURN_REFUND_REJECTED' || order.returnStatus === 'Rejected';
    const isRequested    = isReturnStatus && !isApproved && !isRejected;
    badge.className = `ph-status-pill ${
        isApproved  ? 'ph-s-RETURN_REFUND_APPROVED' :
        isRejected  ? 'ph-s-RETURN_REFUND_REJECTED' :
        isRequested ? 'ph-s-RETURN_REFUND_REJECTED' :
        'ph-s-' + order.status
    }`;
    document.getElementById('odBadgeLabel').textContent =
        isApproved  ? 'Return/Refund Approved' :
        isRejected  ? 'Return/Refund Rejected' :
        isRequested ? 'Return/Refund Requested' :
        order.statusLabel;

    document.getElementById('odDrawerBody').innerHTML = buildDrawerHTML(order);
    document.getElementById('odBackdrop').classList.add('active');
    document.getElementById('odDrawer').classList.add('active');
    document.body.style.overflow = 'hidden';
}

function buildDrawerHTML(order) {
    let html = '';

    if (order.status === 'RETURN_REFUND' && order.returnReason) {
        html += buildReturnNoticeHTML(order);
    }
    if (order.status === 'CANCELED' && order.cancelReason) {
        html += `
        <div class="od-notice od-notice-canceled">
            <i class="ti ti-ban"></i>
            <div><strong>Cancellation Reason</strong><span>${esc(order.cancelReason)}</span></div>
        </div>`;
    }
    if (order.reviewSubmitted) {
        html += buildReviewSummaryHTML(order);
    }

    // ── Items ──────────────────────────────────────────────────
    if (order.items.length === 1) {
        html += `<div class="od-item-detail-panel" id="odActivePanel">`;
        html += buildItemHTML(order, order.items[0]);
        html += `</div>`;
    } else {
        const summaryRows = order.items.map((item, idx) => {
            const thumb  = item.images?.[0] || item.image || '';
            const varStr = item.selectedVariation ? esc(item.selectedVariation) : '';
            return `
            <div class="od-summary-item${idx === 0 ? ' od-sum-active' : ''}"
                 onclick="odSelectItem(this, ${idx}, ${order.orderId})"
                 data-item-idx="${idx}">
                <div class="od-sum-thumb">
                    <img src="${esc(thumb)}" onerror="this.src='/images/placeholder.png'" />
                </div>
                <div class="od-sum-info">
                    <span class="od-sum-name">${esc(item.name)}</span>
                    ${varStr ? `<span class="od-sum-var"><i class="ti ti-tag" style="font-size:10px;margin-right:2px"></i>${varStr}</span>` : ''}
                </div>
                <span class="od-sum-price">RM ${esc(item.subtotal)}</span>
            </div>`;
        }).join('');

        html += `
        <div class="od-items-summary">
            <div class="od-items-summary-header">
                Items in order
                <span class="od-items-summary-count">${order.items.length} items</span>
            </div>
            ${summaryRows}
        </div>`;

        html += `<div class="od-item-detail-panel" id="odActivePanel">`;
        html += buildItemHTML(order, order.items[0]);
        html += `</div>`;
    }

    if (order.customerMessage) {
        html += `
        <div class="od-customer-msg">
            <div class="od-customer-msg-label"><i class="ti ti-message-circle"></i> Note to Seller</div>
            <div class="od-customer-msg-text">${esc(order.customerMessage)}</div>
        </div>`;
    }
    if (order.address) {
        const a = order.address;
        html += `
        <div class="od-address-block">
            <div class="od-address-title"><i class="ti ti-map-pin"></i> Delivery Address</div>
            <div class="od-address-grid">
                <div class="od-address-row"><span>Recipient</span>${esc(a.recipientName)}</div>
                <div class="od-address-row"><span>Phone</span>${esc(a.phoneNumber)}</div>
                <div class="od-address-row"><span>Address</span>${esc(a.line1)}${a.line2 ? ', ' + esc(a.line2) : ''}, ${esc(a.city)}, ${esc(a.postcode)}, ${esc(a.state)}</div>
                <div class="od-address-row"><span>Payment</span>${esc(order.paymentMethod)}</div>
            </div>
        </div>`;
    }
    html += `
    <div class="od-total-bar">
        <span>Order Total</span>
        <strong>RM ${esc(order.totalAmount)}</strong>
    </div>`;

    return html;
}

function odSelectItem(el, itemIdx, orderId) {
    el.closest('.od-items-summary')
      .querySelectorAll('.od-summary-item')
      .forEach(r => r.classList.remove('od-sum-active'));
    el.classList.add('od-sum-active');

    const order = _orders.find(o => o.orderId === orderId);
    const panel = document.getElementById('odActivePanel');
    if (!order || !panel) return;

    // Trigger re-animation
    panel.style.animation = 'none';
    panel.offsetHeight;
    panel.style.animation = '';
    panel.innerHTML = buildItemHTML(order, order.items[itemIdx]);
}

function buildReturnNoticeHTML(order) {
    const imgs = (order.returnImagePath || []).filter(Boolean);
    const photoStrip = imgs.length ? `
        <div class="rn-photo-strip">
            <div class="rn-reason-label">Proof photos</div>
            <div class="rn-photo-row">
                ${imgs.map(src => `
                <div class="rn-strip-thumb" onclick="rtnOpenLightbox('${src.replace(/'/g,"\\'")}')">
                    <img src="${esc(src)}" alt="Return proof" onerror="this.closest('.rn-strip-thumb').style.display='none'" />
                    <div class="rn-img-badge"><i class="ti ti-zoom-in"></i></div>
                </div>`).join('')}
            </div>
        </div>` : '';

    const statusMap = {
        Approved:  'Approved',
        Refunded:  'Refunded',
        Rejected:  'Rejected',
        Requested: 'Requested',
    };
    const dotClass = ['Approved','Refunded'].includes(order.returnStatus)
        ? 'rn-status-dot rn-dot-approved'
        : order.returnStatus === 'Rejected'
            ? 'rn-status-dot rn-dot-rejected'
            : 'rn-status-dot';

    const bannerClass = ['Approved', 'Refunded'].includes(order.returnStatus)
        ? 'ph-notice-return-v2 rn-approved'
        : order.returnStatus === 'Rejected'
            ? 'ph-notice-return-v2 rn-rejected'
            : 'ph-notice-return-v2';

    return `
    <div class="${bannerClass}">
        <div class="rn-header">
            <div class="rn-icon-wrap"><i class="ti ti-package-off"></i></div>
            <div class="rn-header-text">
                <div class="rn-title">Return / Refund Requested</div>
                <div class="rn-status">
                    <span class="${dotClass}"></span>
                    ${esc(statusMap[order.returnStatus] || order.returnStatus || 'Pending Review')}
                </div>
            </div>
        </div>
        <div class="rn-body">
            <div class="rn-reason">
                <div class="rn-reason-label">Your reason</div>
                <div class="rn-reason-text">${esc(order.returnReason)}</div>
            </div>
        </div>
        ${photoStrip}
    </div>`;
}

function buildReviewSummaryHTML(order) {
    const byProduct = new Map();
    order.items
        .filter(item => item.reviewRating > 0)
        .forEach(item => {
            if (!byProduct.has(item.productId)) {
                byProduct.set(item.productId, {
                    representative: item,
                    varLines: [],
                    allImages: [...(item.reviewImages || [])],
                });
            } else {
                const g = byProduct.get(item.productId);
                (item.reviewImages || []).forEach(img => {
                    if (img && !g.allImages.includes(img)) g.allImages.push(img);
                });
            }

            // Collect variation line for this item
            const g = byProduct.get(item.productId);
            if (item.selectedVariation && item.selectedVariation.trim()) {
                const tags = item.selectedVariation.split(' · ').filter(Boolean)
                    .map(p => `<span class="ph-var-tag">${esc(p)}</span>`).join('');
                if (tags) g.varLines.push(`<div class="ph-item-vars" style="margin:2px 0">${tags}</div>`);
            }
        });

    const reviewLines = Array.from(byProduct.values()).map(({ representative: item, varLines, allImages }) => {
        const labels   = ['', 'Poor', 'Fair', 'Good', 'Very Good', 'Excellent'];
        const filled   = '★'.repeat(item.reviewRating);
        const unfilled = '☆'.repeat(5 - item.reviewRating);
        const label    = labels[item.reviewRating] ?? '';

        const textLine = item.reviewText
            ? `<span class="od-reviewed-text">${esc(item.reviewText)}</span>` : '';

        const varBlock = varLines.length
            ? `<div class="od-reviewed-variations">${varLines.join('')}</div>` : '';

        const imgs = allImages.filter(Boolean).slice(0, 4);
        const photoStrip = imgs.length ? `
            <div class="rn-photo-strip" style="margin-top:10px">
                <div class="rn-reason-label">Review photos</div>
                <div class="rn-photo-row">
                    ${imgs.map(src => `
                    <div class="rw-strip-thumb" onclick="rtnOpenLightbox('${String(src).replace(/'/g, "\\'")}')">
                        <img src="${esc(src)}" alt="Review photo" onerror="this.closest('.rw-strip-thumb').style.display='none'" />
                        <div class="rw-img-badge"><i class="ti ti-zoom-in"></i></div>
                    </div>`).join('')}
                </div>
            </div>` : '';

        return `
        <div class="od-reviewed-item">
            <span class="od-reviewed-item-name">${esc(item.name)}</span>
            <span class="od-reviewed-stars">
                <span class="od-reviewed-filled">${filled}</span>
                <span class="od-reviewed-unfilled">${unfilled}</span>
                <span class="od-reviewed-label">${label}</span>
            </span>
            ${varBlock}
            ${textLine}
            ${photoStrip}
        </div>`;
    }).join('');

    return `
    <div class="od-reviewed-sent">
        <i class="ti ti-star"></i>
        <div>
            <strong>Review Submitted</strong>
            ${reviewLines}
        </div>
    </div>`;
}

function buildItemHTML(order, item) {
    const imgs      = (item.images && item.images.length) ? item.images : [item.image];
    const mainImg   = imgs[0] || item.image;
    const fullStars = Math.floor(item.rating);
    const stars     = '★'.repeat(fullStars) + '☆'.repeat(5 - fullStars);

    const thumbsHtml = imgs.length > 1 ? `
        <div class="od-thumbs">
            ${imgs.map((src, i) => `
            <div class="od-thumb ${i===0?'active':''}" onclick="odSwitch(this,'${src.replace(/'/g,"\\'")}')">
                <img src="${esc(src)}" onerror="this.src='/images/placeholder.png'" />
            </div>`).join('')}
        </div>` : '';

    const selectedMap = {};
    if (item.selectedVariation) {
        item.selectedVariation.split(' · ').forEach(pair => {
            const ci = pair.indexOf(': ');
            if (ci > -1) selectedMap[pair.substring(0, ci).trim()] = pair.substring(ci + 2).trim();
        });
    }

    const varHtml = (item.allVariations && item.allVariations.length) ? item.allVariations.map(g => {
        const btns = g.values.map(v => {
            const sel = selectedMap[g.name] === v.label;
            const oos = v.stock <= 0;
            return `<button class="od-var-btn${sel?' sel':oos?' oos':''}" disabled>${esc(v.label)}</button>`;
        }).join('');
        return `<div class="od-var-group"><div class="od-var-name">${esc(g.name)}</div><div class="od-var-opts">${btns}</div></div>`;
    }).join('') : '';

    return `
    <div class="od-item-wrap">
        <div class="od-gallery">
            <div class="od-main-img-wrap">
                <img src="${esc(mainImg)}" alt="${esc(item.name)}" onerror="this.src='/images/placeholder.png'" />
            </div>
            ${thumbsHtml}
        </div>
        <div class="od-info">
            ${item.category ? `<div class="od-category">${esc(item.category)}</div>` : ''}
            <div class="od-name">${esc(item.name)}</div>
            <div class="od-stars-row">
                <span class="od-stars">${stars}</span>
                <span class="od-rating-num">${item.rating.toFixed(1)}</span>
            </div>
            <div class="od-divider"></div>
            <div class="od-shop-row">
                <div class="od-shop-ava">${esc(order.shopInitial)}</div>
                <span>Sold by <strong>${esc(order.shopName)}</strong></span>
            </div>
            <div class="od-price">RM ${esc(item.price)}</div>
            <div class="od-price-note">× ${item.qty} unit${item.qty > 1 ? 's' : ''} = <strong>RM ${esc(item.subtotal)}</strong></div>
            ${item.sku ? `<div class="od-sku">SKU: ${esc(item.sku)}</div>` : ''}
            <div class="od-divider"></div>
            ${item.description ? `<p class="od-desc">${esc(item.description)}</p>` : ''}
            ${varHtml}
        </div>
    </div>`;
}

function odSwitch(thumbEl, src) {
    const gallery = thumbEl.closest('.od-gallery');
    gallery.querySelector('.od-main-img-wrap img').src = src;
    gallery.querySelectorAll('.od-thumb').forEach(t => t.classList.remove('active'));
    thumbEl.classList.add('active');
}

function closeDrawer() {
    document.getElementById('odBackdrop').classList.remove('active');
    document.getElementById('odDrawer').classList.remove('active');
    document.body.style.overflow = '';
}

/* ============================================================
   CANCEL ORDER
   ============================================================ */
function openCancelModal(orderId) {
    document.getElementById('cancelOrderId').value = orderId;
    document.getElementById('cancelCustom').value  = '';
    _cancelSelectedReason = '';
    document.querySelectorAll('#cancelReasonGrid .ph-reason-chip')
        .forEach(c => c.classList.remove('selected'));

    const submitBtn = document.getElementById('cancelModalSubmitBtn');
    const titleEl = document.getElementById('cancelModalTitle');
    const noticeEl = document.getElementById('cancelModalNotice');
    const btnTextEl = document.getElementById('cancelBtnText');

    titleEl.innerText = "Cancel Order";
    btnTextEl.innerText = "Confirm Cancel";
    noticeEl.style.display = "none"; 
    submitBtn.dataset.isRequest = "false";

    showModal('cancelBackdrop', 'cancelModal');
}

function closeCancelModal() { hideModal('cancelBackdrop', 'cancelModal'); }

function selectReason(chip, textareaId) {
    chip.parentElement.querySelectorAll('.ph-reason-chip')
        .forEach(c => c.classList.remove('selected'));
    chip.classList.add('selected');
    _cancelSelectedReason = chip.textContent.trim();
    const ta = document.getElementById(textareaId);
    if (!ta.value.trim()) ta.value = _cancelSelectedReason === 'Other' ? '' : _cancelSelectedReason;
}

function closeRequestCancelModal() { hideModal('requestCancelBackdrop', 'requestCancelModal'); }

async function submitCancel() {
    const orderId = document.getElementById('cancelOrderId').value;
    const reason  = document.getElementById('cancelCustom').value.trim() || _cancelSelectedReason;

    if (!reason) {
        showInlineError('cancelModal', 'Please select or enter a cancellation reason.');
        return;
    }

    const btn = document.getElementById('cancelModalSubmitBtn');
    const originalText = btn.innerHTML;
    btn.innerHTML = '<i class="ti ti-loader-2 spin"></i> Processing…';

    try {
        const res = await postJson('/Customer/CancelOrder', { orderId, cancelReason: reason});
        if (res.success) {
            closeCancelModal();
            const successMsg = 'Order canceled successfully.';           
                
            showToast(successMsg, 'success');
            setTimeout(() => location.reload(), 1200);
        } else {
            btn.disabled = false;
            btn.innerHTML = '<i class="ti ti-x"></i> Confirm Cancel';
            showInlineError('cancelModal', res.message || 'Could not cancel order.');
        }
    } catch (err) {
        btn.disabled = false;
        btn.innerHTML = '<i class="ti ti-x"></i> Confirm Cancel';
        showInlineError('cancelModal', 'Network error — please try again.');
        console.error('CancelOrder error:', err);
    }
}

/* ============================================================
   CONFIRM RECEIVED
   ============================================================ */
async function confirmReceived(orderId, btn) {
    if (!confirm('Confirm that you have received this order?')) return;
    btn.disabled = true;
    btn.innerHTML = '<i class="ti ti-loader-2 spin"></i> Confirming…';

    try {
        const res = await postJson('/Customer/ConfirmReceived', { orderId });
        if (res.success) {
            showToast('Order marked as received!', 'success');
            setTimeout(() => location.reload(), 1200);
        } else {
            btn.disabled = false;
            btn.innerHTML = '<i class="ti ti-circle-check"></i> Order Received';
            showToast(res.message || 'Failed to confirm.', 'error');
        }
    } catch (err) {
        btn.disabled = false;
        btn.innerHTML = '<i class="ti ti-circle-check"></i> Order Received';
        showToast('Network error, please try again.', 'error'); // ← 加这行
        console.error('ConfirmReceived error:', err);
    }
}

/* ============================================================
   RETURN / REFUND MODAL
   ============================================================ */
function openReturnModal(orderId) {
    const order = _orders.find(o => o.orderId === orderId);
    if (!order) return;
    currentReturnOrderId = orderId;

    _rtnOrderItems  = order.items.map(it => ({
        orderItemId:       it.orderItemId,
        name:              it.name,
        image:             it.image,
        qty:               it.qty,
        selectedVariation: it.selectedVariation || '',
        sku:               it.sku || '',
    }));
    _rtnFiles          = [];
    _rtnSelectedType   = '';
    _rtnSelectedReason = '';


    document.getElementById('returnOrderId').value = orderId;
    document.getElementById('returnReason').value  = '';
    document.querySelectorAll('.rtn-type-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.rtn-reason-chip').forEach(b => b.classList.remove('selected'));

    rtnRenderItemList();
    rtnRenderPreviews();
    showModal('returnBackdrop', 'returnModal');
}

/* ── Variation JSON → readable line (shared by request modals) ─────── */
function parseVariation(json) {
    if (!json || json === '{}' || json === '[]' || json === '') return '';
    try {
        const obj = typeof json === 'string' ? JSON.parse(json) : json;
        return Object.entries(obj).map(([k, v]) => `${k}: ${v}`).join(' · ');
    } catch { return ''; }
}

// Builds the full variation block for a deduplicated product group.
// groups = [{ item, totalQty }] — all line items for the same productId
function buildProductVariationBlockHTML(itemGroups) {
    const hasAnyVar = itemGroups.some(g =>
        g.item.selectedVariation && g.item.selectedVariation.trim() !== ''
    );

    if (!hasAnyVar) {
        // No variations at all — just total qty badge if > 1
        const totalQty = itemGroups.reduce((s, g) => s + g.totalQty, 0);
        return totalQty > 1
            ? `<div style="margin-top:3px"><span class="ph-rating-qty-badge">×${totalQty}</span></div>`
            : '';
    }

    const rows = itemGroups.map(g => {
        const pairs = (g.item.selectedVariation || '').split(' · ').filter(Boolean);
        const tags  = pairs.map(p => `<span class="ph-var-tag">${esc(p)}</span>`).join('');
        const qty   = g.totalQty > 1
            ? `<span class="ph-rating-qty-badge">×${g.totalQty}</span>`
            : '';
        return `<div class="ph-item-vars" style="margin-top:4px">${tags}${qty}</div>`;
    }).join('');

    return rows;
}

async function openRequestModal(orderId) {
    showModal('requestDetailBackdrop', 'requestDetailModal');
    document.getElementById('requestDetailBody').innerHTML = `
        <div style="text-align:center; padding: 40px;">
            <i class="ti ti-loader" style="font-size:28px;"></i>
        </div>`;

    const res = await fetch(`/Request/GetRequest?orderId=${orderId}`);
    const result = await res.json();

    if (!result.success) {
        document.getElementById('requestDetailBody').innerHTML = `<p style="color:red;">${result.message}</p>`;
        return;
    }

    let totalRefund = 0;
    const itemsHtml = result.orderItems.map(item => {
        const unitPrice  = parseFloat(item.discountedPrice ?? item.price);
        const reqQty     = item.requestedQty ?? item.quantity;
        const lineTotal  = unitPrice * reqQty;
        totalRefund     += lineTotal;
        const varLine    = parseVariation(item.selectedVariation);
        return `
        <div style="display:flex;align-items:center;gap:10px;padding:10px 0;border-bottom:0.5px solid #ebebf5;">
            <div style="width:44px;height:44px;border-radius:8px;overflow:hidden;flex-shrink:0;background:#f0f0f8;display:flex;align-items:center;justify-content:center;">
                ${item.imageUrl
                    ? `<img src="${item.imageUrl}" style="width:100%;height:100%;object-fit:cover;">`
                    : `<i class="ti ti-box" style="font-size:20px;color:#9a98b6;"></i>`}
            </div>
            <div style="flex:1;min-width:0;">
                <p style="margin:0 0 2px;font-size:13px;font-weight:600;color:#1e1b4b;">${item.productName}</p>
                ${varLine ? `<p style="margin:0 0 2px;font-size:11px;color:#6366f1;"><i class="ti ti-tag" style="margin-right:3px"></i>${varLine}</p>` : ''}
                <p style="margin:0;font-size:11px;color:#9a98b6;">
                    Qty ordered: <strong style="color:#6b6b8a;">${item.quantity}</strong>
                    &nbsp;·&nbsp;
                    Requested: <strong style="color:#f97316;">${reqQty}</strong>
                    &nbsp;·&nbsp;
                    RM ${unitPrice.toFixed(2)} / unit
                </p>
            </div>
        </div>`;
    }).join('');

    const refundSummaryHtml = `
    <div style="display:flex;justify-content:space-between;align-items:center;padding:10px 0 0;margin-top:4px;border-top:1.5px solid #e8eaf6;">
        <p style="margin:0;font-size:12px;font-weight:700;color:#6b6b8a;text-transform:uppercase;letter-spacing:0.05em;">Total Refund Amount</p>
        <p style="margin:0;font-size:16px;font-weight:800;color:#f97316;">RM ${totalRefund.toFixed(2)}</p>
    </div>`;

    const imagesHtml = result.images?.length
        ? result.images.map(url => `<img src="${url}" style="width:68px;height:68px;object-fit:cover;border-radius:8px;border:0.5px solid #ebebf5;">`).join('')
        : '<span style="font-size:13px;color:#9a98b6;">No photos attached</span>';

    document.getElementById('requestDetailBody').innerHTML = `
    <div style="background:#f8f8fc;border-radius:10px;padding:10px 14px;display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;">
        <div>
            <p style="margin:0 0 2px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Request submitted</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#1e1b4b;">${result.createdAt}</p>
        </div>
        <i class="ti ti-clock" style="font-size:20px;color:#9a98b6;"></i>
    </div>

    ${(() => {
        const isRejected = result.status === 'Rejected' || !!result.rejectedAt;
        const solvedDate = result.approvedAt || result.rejectedAt;
        if (!solvedDate) {
            return `
    <div style="background:#fffbeb;border-radius:10px;padding:10px 14px;display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;border:1px solid #fde68a;">
        <div>
            <p style="margin:0 0 2px;font-size:11px;font-weight:700;color:#b45309;text-transform:uppercase;letter-spacing:0.06em;">Request Approval</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#92400e;">Pending — awaiting customer service</p>
        </div>
        <i class="ti ti-hourglass" style="font-size:20px;color:#f59e0b;"></i>
    </div>`;
        }
        if (isRejected) {
            return `
    <div style="background:#fff1f2;border-radius:10px;padding:10px 14px;display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;border:1px solid #fecdd3;">
        <div>
            <p style="margin:0 0 2px;font-size:11px;font-weight:700;color:#b91c1c;text-transform:uppercase;letter-spacing:0.06em;">Request Rejected</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#991b1b;">${solvedDate}${result.rejectionReason ? ' · ' + result.rejectionReason : ''}</p>
        </div>
        <i class="ti ti-circle-x" style="font-size:20px;color:#ef4444;"></i>
    </div>`;
        }
        return `
    <div style="background:#f0fdf4;border-radius:10px;padding:10px 14px;display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;border:1px solid #bbf7d0;">
        <div>
            <p style="margin:0 0 2px;font-size:11px;font-weight:700;color:#15803d;text-transform:uppercase;letter-spacing:0.06em;">Request Approved</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#166534;">${solvedDate}</p>
        </div>
        <i class="ti ti-circle-check" style="font-size:20px;color:#22c55e;"></i>
    </div>`;
    })()}

    <div style="background:#f8f8fc;border-radius:10px;padding:12px 14px;margin-bottom:10px;">
        <p style="margin:0 0 8px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Order items</p>
        ${itemsHtml}
        ${refundSummaryHtml}
    </div>

    <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:10px;">
        <div style="background:#f8f8fc;border-radius:10px;padding:10px 14px;">
            <p style="margin:0 0 3px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Service type</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#1e1b4b;">${result.serviceType}</p>
        </div>
        <div style="background:#f8f8fc;border-radius:10px;padding:10px 14px;">
            <p style="margin:0 0 3px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Reason</p>
            <p style="margin:0;font-size:13px;font-weight:500;color:#1e1b4b;">${result.issueType}</p>
        </div>
    </div>

    <div style="background:#f8f8fc;border-radius:10px;padding:10px 14px;margin-bottom:10px;">
        <p style="margin:0 0 3px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Description</p>
        <p style="margin:0;font-size:13px;color:#1e1b4b;line-height:1.5;">${result.description || '—'}</p>
    </div>

    <div style="background:#f8f8fc;border-radius:10px;padding:10px 14px;">
        <p style="margin:0 0 8px;font-size:11px;font-weight:700;color:#9a98b6;text-transform:uppercase;letter-spacing:0.06em;">Photos</p>
        <div style="display:flex;gap:8px;flex-wrap:wrap;">${imagesHtml}</div>
    </div>
`;
}

function closeRequestModal() {
    hideModal('requestDetailBackdrop', 'requestDetailModal'); 
}

function rtnRenderItemList() {
    const container = document.getElementById('rtnItemList');
    if (!container) return;
    container.innerHTML = _rtnOrderItems.map((item, idx) => {

        // Parse variation string into individual tags
        const varTagsHtml = item.selectedVariation
            ? item.selectedVariation.split(' · ').filter(Boolean)
                .map(p => `<span class="ph-var-tag">${esc(p)}</span>`).join('')
            : '';

        return `
        <div class="rtn-item-row" data-idx="${idx}">
            <label class="rtn-item-check-wrap">
                <input type="checkbox" class="rtn-item-checkbox"
                       data-order-item-id="${item.orderItemId}"
                       onchange="rtnToggleItemRow(this)" checked />
                <img class="rtn-item-img" src="${esc(item.image)}" alt="${esc(item.name)}"
                     onerror="this.src='/images/placeholder.png'" />
                <div class="rtn-item-info">
                    <div class="rtn-item-name">${esc(item.name)}</div>
                    ${varTagsHtml ? `<div class="ph-item-vars" style="margin-top:3px">${varTagsHtml}</div>` : ''}
                    ${item.sku ? `<div class="rtn-item-sku"><i class="ti ti-barcode" style="font-size:10px;margin-right:3px"></i>${esc(item.sku)}</div>` : ''}
                    <div class="rtn-item-ordered">Quantity: <strong>${item.qty}</strong></div>
                </div>
            </label>
            <div class="rtn-item-qty-wrap">
                <span class="rtn-item-qty-label">Qty:</span>
                <input type="number" class="rtn-qty-input"
                       data-order-item-id="${item.orderItemId}"
                       min="1" max="${item.qty}" value="1" />
            </div>
        </div>`;
    }).join('');
}

function rtnToggleItemRow(checkbox) {
    const row = checkbox.closest('.rtn-item-row');
    row.querySelector('.rtn-qty-input').disabled = !checkbox.checked;
    row.classList.toggle('rtn-item-row-disabled', !checkbox.checked);
}

function rtnSelectType(btn) {
    document.querySelectorAll('.rtn-type-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    _rtnSelectedType = btn.dataset.type;
}

function rtnSelectReason(btn) {
    document.querySelectorAll('.rtn-reason-chip').forEach(b => b.classList.remove('selected'));
    btn.classList.add('selected');
    _rtnSelectedReason = btn.dataset.value;
}

function closeReturnModal() {
    hideModal('returnBackdrop', 'returnModal');
    _rtnFiles = [];
    rtnRenderPreviews();
}

function rtnHandleFiles(fileList) {
    Array.from(fileList).forEach(f => {
        if (_rtnFiles.length >= 4) return;
        if (!f.type.startsWith('image/')) return;
        if (f.size > 5 * 1024 * 1024) { showInlineError('returnModal', `"${f.name}" exceeds 5 MB.`); return; }
        _rtnFiles.push(f);
    });
    document.getElementById('returnImage').value = '';
    rtnRenderPreviews();
}

function rtnHandleDrop(e) {
    e.preventDefault();
    document.getElementById('rtnUploadZone').classList.remove('drag-over');
    rtnHandleFiles(e.dataTransfer.files);
}

function rtnRemoveFile(idx) {
    _rtnFiles.splice(idx, 1);
    rtnRenderPreviews();
}

function rtnRenderPreviews() {
    const row = document.getElementById('rtnPreviewRow');
    if (!row) return;
    row.innerHTML = _rtnFiles.map((f, i) => {
        const url  = URL.createObjectURL(f);
        const name = f.name.length > 18 ? f.name.slice(0, 15) + '…' : f.name;
        return `
        <div class="rtn-preview-chip">
            <img src="${url}" alt="${esc(name)}" />
            <span>${esc(name)}</span>
            <button class="rtn-chip-remove" onclick="rtnRemoveFile(${i})" aria-label="Remove photo">
                <i class="ti ti-x"></i>
            </button>
        </div>`;
    }).join('');
}


async function submitReturn() {
    const orderId = currentReturnOrderId;
    const description = document.getElementById('returnReason').value.trim();

    if (!description) {
        showInlineError('returnModal', 'Please describe your issue.');
        return;
    }

    const checkedBoxes = document.querySelectorAll('#rtnItemList .rtn-item-checkbox:checked');
    if (!checkedBoxes.length) {
        showInlineError('returnModal', 'Please select at least one item.');
        return;
    }

    if (!_rtnSelectedType) {
        showInlineError('returnModal', 'Please select service type.');
        return;
    }

    if (!_rtnSelectedReason) {
        showInlineError('returnModal', 'Please select issue type.');
        return;
    }

    const items = Array.from(checkedBoxes).map(cb => {
        const row = cb.closest('.rtn-item-row');
        return {
            orderItemId: parseInt(cb.dataset.orderItemId),
            qty: parseInt(row.querySelector('.rtn-qty-input').value || 1)
        };
    });

    const formData = new FormData();
    formData.append('orderId', orderId);
    formData.append('requestServiceType', _rtnSelectedType);   
    formData.append('requestIssueType', _rtnSelectedReason);   
    formData.append('description', description);

    items.forEach(item => {                                     
        formData.append('requestItemIds', item.orderItemId);
        formData.append('requestItemQtys', item.qty);
    });

    _rtnFiles.forEach(file => {
        formData.append('images', file);
    });

    // Anti-forgery token required by [ValidateAntiForgeryToken]
    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    if (csrfToken) formData.append('__RequestVerificationToken', csrfToken);

    const btn = document.querySelector('#returnModal .ph-btn-return');
    btn.disabled = true;
    btn.innerHTML = '<i class="ti ti-loader-2 spin"></i> Submitting...';

    try {
        const res = await fetch('/Request/CreateAfterSalesRequest', {
            method: 'POST',
            body: formData
        });

        const data = await res.json();

        if (data.success) {
            closeReturnModal();
            showToast('Request submitted successfully!', 'success');
            setTimeout(() => location.reload(), 1000);
            return;
        } else {
            showInlineError('returnModal', data.message || 'Failed to submit request.');
        }
    } catch (err) {
        console.error(err);
        showInlineError('returnModal', 'Network error. Please try again.');
    }

    btn.disabled = false;
    btn.innerHTML = '<i class="ti ti-send"></i> Submit Request';
}

/* ============================================================
   LIGHTBOX
   ============================================================ */
function rtnOpenLightbox(src) {
    document.getElementById('rtnLightboxImg').src = src;
    document.getElementById('rtnLightbox').classList.add('active');
    document.body.style.overflow = 'hidden';
}

function rtnCloseLightbox() {
    document.getElementById('rtnLightbox').classList.remove('active');
    document.body.style.overflow = '';
}

/* ============================================================
   RATING / REVIEW MODAL
   ============================================================ */
function openRatingModal(orderId) {
    const order = _orders.find(o => o.orderId === orderId);
    if (!order) return;

    document.getElementById('ratingOrderId').value = orderId;
    _ratingFilesByOrderItem = {};

    const byProduct = new Map(); 
    order.items.forEach(item => {
        if (!byProduct.has(item.productId)) {
            byProduct.set(item.productId, { item, varGroups: new Map() });
        }
        const prod   = byProduct.get(item.productId);
        const varKey = item.selectedVariation || '';
        if (!prod.varGroups.has(varKey)) {
            prod.varGroups.set(varKey, { item, allOrderItemIds: [item.orderItemId], totalQty: item.qty });
        } else {
            const vg = prod.varGroups.get(varKey);
            vg.allOrderItemIds.push(item.orderItemId);
            vg.totalQty += item.qty;
        }
    });

    const productGroups = Array.from(byProduct.values());

    document.getElementById('ratingItemsWrap').innerHTML = productGroups.map((prod, idx) => {
        const allOrderItemIds = Array.from(prod.varGroups.values())
            .flatMap(vg => vg.allOrderItemIds);
        const primaryId = allOrderItemIds[0];
        _ratingFilesByOrderItem[primaryId] = [];

        const groupJson   = esc(JSON.stringify(allOrderItemIds));
        const varGroups   = Array.from(prod.varGroups.values());
        const varBlockHTML = buildProductVariationBlockHTML(varGroups);

        return `
        <div class="ph-rating-item"
             data-item-idx="${idx}"
             data-product-id="${prod.item.productId}"
             data-primary-order-item-id="${primaryId}"
             data-all-order-item-ids="${groupJson}">

            <div class="ph-rating-item-top">
                <img src="${esc(prod.item.image)}" onerror="this.src='/images/placeholder.png'" />
                <div class="ph-rating-item-meta">
                    <span class="ph-rating-item-name">${esc(prod.item.name)}</span>
                    ${varBlockHTML}
                    ${prod.item.sku ? `<span class="ph-rating-item-sku" style="margin-top:3px"><i class="ti ti-barcode" style="margin-right:2px"></i>${esc(prod.item.sku)}</span>` : ''}
                </div>
            </div>

            <div class="ph-star-picker" data-selected="0">
                ${[1,2,3,4,5].map(n =>
                    `<button class="ph-star-btn" data-val="${n}" onclick="pickStar(this)">★</button>`
                ).join('')}
                <span class="ph-star-hint">Tap to rate</span>
            </div>

            <div class="ph-textarea-wrap" style="margin-top:10px">
                <textarea class="ph-review-text" placeholder="Share your experience (optional)…" rows="2"></textarea>
            </div>

            <div class="ph-review-upload" style="margin-top:12px">
                <label class="rtn-field-label" style="display:block;margin-bottom:6px">Review photos (optional)</label>
                <div class="rw-upload-zone"
                     ondragover="event.preventDefault();this.classList.add('drag-over')"
                     ondragleave="this.classList.remove('drag-over')"
                     ondrop="ratingHandleDrop(event,${primaryId})">
                    <input type="file" class="ph-review-images" accept="image/*" multiple
                           onchange="ratingHandleFiles(this.files,${primaryId});this.value='';" />
                    <div class="rw-upload-inner">
                        <div class="rw-upload-icon"><i class="ti ti-camera"></i></div>
                        <div class="rw-upload-text"><strong>Click to upload</strong> or drag photos here</div>
                        <div class="rw-upload-hint">JPG, PNG, WEBP · Max 5 MB each · Up to 4 photos</div>
                    </div>
                </div>
                <div class="rw-preview-row" id="ratingPreviewRow_${primaryId}"></div>
            </div>
        </div>
        ${idx < productGroups.length - 1 ? '<div class="od-sep"></div>' : ''}`;
    }).join('');

    showModal('ratingBackdrop', 'ratingModal');
}

function pickStar(btn) {
    const picker = btn.closest('.ph-star-picker');
    const val    = parseInt(btn.dataset.val);
    picker.dataset.selected = val;
    picker.querySelectorAll('.ph-star-btn').forEach((b, i) => b.classList.toggle('filled', i < val));
    const labels = ['','Poor','Fair','Good','Very Good','Excellent'];
    picker.querySelector('.ph-star-hint').textContent = labels[val] || '';
}

function ratingHandleFiles(fileList, orderItemId) {
    const arr = (_ratingFilesByOrderItem[orderItemId] ??= []);
    Array.from(fileList || []).forEach(f => {
        if (arr.length >= 4) return;
        if (!f.type.startsWith('image/')) return;
        if (f.size > 5 * 1024 * 1024) { showInlineError('ratingModal', `"${f.name}" exceeds 5 MB.`); return; }
        arr.push(f);
    });
    ratingRenderPreviews(orderItemId);
}

function ratingHandleDrop(e, orderItemId) {
    e.preventDefault();
    e.currentTarget.classList.remove('drag-over');
    ratingHandleFiles(e.dataTransfer.files, orderItemId);
}

function ratingRemoveFile(orderItemId, idx) {
    (_ratingFilesByOrderItem[orderItemId] ?? []).splice(idx, 1);
    ratingRenderPreviews(orderItemId);
}

function ratingRenderPreviews(orderItemId) {
    const row = document.getElementById(`ratingPreviewRow_${orderItemId}`);
    if (!row) return;
    const arr = _ratingFilesByOrderItem[orderItemId] ?? [];
    row.innerHTML = arr.map((f, i) => {
        const url  = URL.createObjectURL(f);
        const name = f.name.length > 18 ? f.name.slice(0, 15) + '…' : f.name;
        return `
        <div class="rw-preview-chip">
            <img src="${url}" alt="${esc(name)}" />
            <span>${esc(name)}</span>
            <button class="rw-chip-remove" onclick="ratingRemoveFile(${orderItemId},${i})" aria-label="Remove">
                <i class="ti ti-x"></i>
            </button>
        </div>`;
    }).join('');
}

function closeRatingModal() { hideModal('ratingBackdrop', 'ratingModal'); }

async function submitRating() {
    const orderId  = document.getElementById('ratingOrderId').value;
    const items    = document.querySelectorAll('.ph-rating-item');
    let   hasError = false;
    let   allOk    = true;
    const submitted = [];

    for (const item of items) {
        const rating      = parseInt(item.querySelector('.ph-star-picker').dataset.selected);
        const reviewText  = item.querySelector('.ph-review-text').value.trim();
        const primaryId   = parseInt(item.dataset.primaryOrderItemId);
        const allIds      = JSON.parse(item.dataset.allOrderItemIds || `[${primaryId}]`);

        if (!rating) { hasError = true; continue; }

        for (const orderItemId of allIds) {
            try {
                const formData = new FormData();
                formData.append('orderItemId', orderItemId);
                formData.append('rating', rating);
                formData.append('reviewText', reviewText);
                if (orderItemId === primaryId) {
                    (_ratingFilesByOrderItem[primaryId] ?? []).slice(0, 4)
                        .forEach(f => formData.append('images', f));
                }
                formData.append('__RequestVerificationToken',
                    document.querySelector('input[name="__RequestVerificationToken"]').value);

                const res  = await fetch('/Customer/SubmitRating', { method: 'POST', body: formData });
                const data = await res.json();

                if (!data.success) {
                    allOk = false;
                    showInlineError('ratingModal', data.message || 'Could not save review.');
                }
            } catch {
                allOk = false;
                showInlineError('ratingModal', 'Network error — please try again.');
            }
        }
        submitted.push({ primaryId, allIds, rating, reviewText });
    }

    if (hasError) {
        showInlineError('ratingModal', 'Please give at least 1 star for each item.');
        return;
    }

    const order = _orders.find(o => o.orderId === parseInt(orderId));
    if (order) {
        submitted.forEach(({ primaryId, allIds, rating, reviewText }) => {
            allIds.forEach(id => {
                const it = order.items.find(i => i.orderItemId === id);
                if (it) { it.reviewRating = rating; it.reviewText = reviewText; }
            });
            const primary = order.items.find(i => i.orderItemId === primaryId);
            if (primary) {
                const files = _ratingFilesByOrderItem[primaryId] ?? [];
                primary.reviewImages = files.map(f => URL.createObjectURL(f));
            }
        });
        order.reviewSubmitted = true;
    }

    closeRatingModal();
    if (allOk) {
        markReviewWritten(parseInt(orderId));
        showToast('Review submitted, thank you!', 'success');
    } else {
        showToast('Some reviews could not be saved.', 'error');
    }
}

function markReviewWritten(orderId) {
    const order = _orders.find(o => o.orderId === orderId);
    if (order) order.reviewSubmitted = true;

    const card = document.querySelector(`.ph-card[data-order-id="${orderId}"]`);
    if (!card) return;

    const btn = card.querySelector('.js-write-review');
    if (btn) {
        btn.outerHTML = `<span class="ph-btn ph-btn-done"><i class="ti ti-check"></i> Review Submitted</span>`;
    }
}

/* ============================================================
   SHARED HELPERS
   ============================================================ */
function esc(str) {
    const d = document.createElement('div');
    d.textContent = str ?? '';
    return d.innerHTML;
}

async function postJson(url, data) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    const body  = new URLSearchParams({ ...data, __RequestVerificationToken: token });
    const res   = await fetch(url, {
        method:  'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body:    body.toString(),
    });

    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
}

function showModal(backdropId, modalId) {
    document.getElementById(backdropId).classList.add('active');
    document.getElementById(modalId).classList.add('active');
    document.body.style.overflow = 'hidden';
}

function hideModal(backdropId, modalId) {
    document.getElementById(backdropId).classList.remove('active');
    document.getElementById(modalId).classList.remove('active');
    document.body.style.overflow = '';
    document.querySelectorAll(`#${modalId} .ph-inline-err`).forEach(el => el.remove());
}

function showInlineError(modalId, msg) {
    const modal = document.getElementById(modalId);
    let err = modal.querySelector('.ph-inline-err');
    if (!err) {
        err = document.createElement('div');
        err.className = 'ph-inline-err';
        modal.querySelector('.ph-modal-footer').before(err);
    }
    err.innerHTML = `<i class="ti ti-alert-circle"></i> ${msg}`;
}

function showToast(msg, type = 'success') {
    const t = document.createElement('div');
    t.className = `ph-toast${type === 'error' ? ' ph-toast-err' : ''}`;
    t.innerHTML = `<i class="ti ${type === 'success' ? 'ti-circle-check' : 'ti-alert-circle'}"></i> ${msg}`;
    document.body.appendChild(t);
    setTimeout(() => t.classList.add('ph-toast-hide'), 3500);
    setTimeout(() => t.remove(), 4200);
}