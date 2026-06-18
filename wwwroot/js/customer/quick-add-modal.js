(function () {
    /* ══════════════════════════════════════════════════
       STATE
    ══════════════════════════════════════════════════ */
    let allCombos    = [];
    let groupNames   = [];
    let selectedKeys = [];

    /* ══════════════════════════════════════════════════
       DOM REFS
    ══════════════════════════════════════════════════ */
    const backdrop     = document.getElementById('qaBackdrop');
    const drawer       = document.getElementById('qaDrawer');
    const skeleton     = document.getElementById('qaDrawerSkeleton');
    const closeBtn     = document.getElementById('qaClose');
    const mainImg      = document.getElementById('qaMainImg');
    const thumbsWrap   = document.getElementById('qaThumbs');
    const shopEl       = document.getElementById('qaShopName');
    const badge        = document.getElementById('qaStockBadge');
    const nameEl       = document.getElementById('qaProductName');
    const priceEl      = document.getElementById('qaPrice');
    const originalPriceEl = document.getElementById('qaOriginalPrice');
    const discountEl      = document.getElementById('qaDiscountBadge');
    const descEl       = document.getElementById('qaDescription');
    const varWrap      = document.getElementById('qaVariations');
    const varNote      = document.getElementById('qaVariationNote');
    const qtyInput     = document.getElementById('qaQtyInput');
    const qtyMinus     = document.getElementById('qaQtyMinus');
    const qtyPlus      = document.getElementById('qaQtyPlus');
    const stockNote    = document.getElementById('qaStockNote');
    const form         = document.getElementById('qaForm');
    const productIdEl  = document.getElementById('qaProductId');
    const selVarsEl    = document.getElementById('qaSelVars');
    const qtyHiddenEl  = document.getElementById('qaQtyHidden');
    const submitBtn    = document.getElementById('qaSubmit');
    const viewFullLink = document.getElementById('qaViewFull');
    const qaImgWrap       = document.getElementById('qaImgWrap');
    const qaImgLightbox   = document.getElementById('qaImgLightbox');
    const qaImgLightboxImg = document.getElementById('qaImgLightboxImg');
    const qaImgLightboxClose = document.getElementById('qaImgLightboxClose');

    /* ══════════════════════════════════════════════════
       GALLERY IMAGE LIGHTBOX
    ══════════════════════════════════════════════════ */
    function openGalleryLightbox() {
        qaImgLightboxImg.src = mainImg.src;
        qaImgLightbox.hidden = false;
        requestAnimationFrame(() => qaImgLightbox.classList.add('open'));
        document.body.style.overflow = 'hidden';
    }
    function closeGalleryLightbox() {
        qaImgLightbox.classList.remove('open');
        qaImgLightbox.addEventListener('transitionend', () => {
            qaImgLightbox.hidden = true;
            if (drawer.style.display === 'none') document.body.style.overflow = '';
        }, { once: true });
    }

    qaImgWrap.addEventListener('click', openGalleryLightbox);
    qaImgLightboxClose.addEventListener('click', e => { e.stopPropagation(); closeGalleryLightbox(); });
    qaImgLightbox.addEventListener('click', e => { if (e.target === qaImgLightbox) closeGalleryLightbox(); });

    /* ══════════════════════════════════════════════════
       TOAST
    ══════════════════════════════════════════════════ */
    function toast(msg, type = 'success') {
        const t = document.createElement('div');
        t.className = 'qa-toast ' + type;
        t.textContent = msg;
        document.body.appendChild(t);
        setTimeout(() => {
            t.classList.add('out');
            setTimeout(() => t.remove(), 350);
        }, 2800);
    }

    /* ══════════════════════════════════════════════════
       PANEL OPEN / CLOSE
    ══════════════════════════════════════════════════ */
    function openPanel(el) {
        el.style.display = 'flex';
        requestAnimationFrame(() => el.classList.add('open'));
    }
    function closePanel(el) {
        el.classList.remove('open');
        setTimeout(() => { el.style.display = 'none'; }, 340);
    }
    function closeAll() {
        backdrop.classList.remove('open');
        closePanel(drawer);
        closePanel(skeleton);
        document.body.style.overflow = '';
    }

    closeBtn.addEventListener('click', closeAll);
    backdrop.addEventListener('click', closeAll);
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            if (qaImgLightbox && !qaImgLightbox.hidden) { closeGalleryLightbox(); return; }
            closeAll();
        }
    });

    /* ══════════════════════════════════════════════════
       QUANTITY
    ══════════════════════════════════════════════════ */
    function clampQty(v) {
        const max = Math.max(1, parseInt(qtyInput.max || '1', 10));
        return Math.min(max, Math.max(1, v));
    }
    function setQty(v) {
        const c = clampQty(v);
        qtyInput.value    = c;
        qtyHiddenEl.value = c;
    }
    qtyMinus.addEventListener('click', () => setQty(parseInt(qtyInput.value, 10) - 1));
    qtyPlus .addEventListener('click', () => setQty(parseInt(qtyInput.value, 10) + 1));

    /* ══════════════════════════════════════════════════
       BADGE
    ══════════════════════════════════════════════════ */
    function setBadge(cls, text) {
        badge.className   = 'qa-badge ' + cls;
        badge.textContent = text;
    }

    /* ══════════════════════════════════════════════════
       COMBO HELPERS
    ══════════════════════════════════════════════════ */
    function findComboStock() {
        const m = allCombos.find(c =>
            Array.isArray(c.keys) &&
            c.keys.length === groupNames.length &&
            selectedKeys.every((k, i) => c.keys[i] === k)
        );
        return m ? (m.stock || 0) : 0;
    }

    function comboReachable(partialKeys) {
        return allCombos.some(c => {
            if (!Array.isArray(c.keys) || c.keys.length !== groupNames.length) return false;
            for (let i = 0; i < groupNames.length; i++) {
                if (partialKeys[i] !== null && c.keys[i] !== partialKeys[i]) return false;
            }
            return (c.stock || 0) > 0;
        });
    }

    function updateHidden() {
        const obj = {};
        groupNames.forEach((n, i) => { if (selectedKeys[i] !== null) obj[n] = selectedKeys[i]; });
        selVarsEl.value = JSON.stringify(obj);
    }

    /* ══════════════════════════════════════════════════
       VARIATION UI
    ══════════════════════════════════════════════════ */
    function updateOptionAvailability() {
        groupNames.forEach((_, gi) => {
            const gEl = varWrap.querySelector('[data-qa-gi="' + gi + '"]');
            if (!gEl) return;
            gEl.querySelectorAll('[data-qa-label]').forEach(opt => {
                const trial = selectedKeys.slice();
                trial[gi]   = opt.getAttribute('data-qa-label');
                const avail = comboReachable(trial);
                opt.classList.toggle('unavailable', !avail);
                if (opt.tagName === 'BUTTON') {
                    opt.disabled = !avail;
                } else {
                    opt.setAttribute('aria-disabled', String(!avail));
                    opt.style.pointerEvents = avail ? 'auto' : 'none';
                }
            });
        });
    }

    function updateVariationUI() {
        const allSel = selectedKeys.every(v => v !== null);
        updateHidden();

        if (groupNames.length === 0) return;

        updateOptionAvailability();

        if (!allSel) {
            setBadge('select-options', 'Select Options');
            varNote.style.display = 'none';
            submitBtn.disabled    = true;
            qtyMinus.disabled     = true;
            qtyPlus.disabled      = true;
            return;
        }

        const stock = findComboStock();
        if (stock <= 0)      setBadge('out-of-stock', 'Out of Stock');
        else if (stock <= 5) setBadge('low-stock',    'Low Stock');
        else                 setBadge('in-stock',      'In Stock');

        varNote.style.display = 'block';
        varNote.textContent   = stock > 0
            ? stock + ' left for this option'
            : 'This combination is out of stock';

        qtyInput.max       = Math.max(1, stock);
        setQty(parseInt(qtyInput.value, 10));

        const ok           = stock > 0;
        submitBtn.disabled = !ok;
        qtyMinus.disabled  = !ok;
        qtyPlus.disabled   = !ok;
    }

    function handleVarClick(el) {
        const gi    = parseInt(el.getAttribute('data-qa-gi'), 10);
        const label = el.getAttribute('data-qa-label');
        if (isNaN(gi) || !label) return;

        const gEl = varWrap.querySelector('[data-qa-gi="' + gi + '"]');
        gEl.querySelectorAll('.qa-var-btn, .qa-var-swatch').forEach(x => x.classList.remove('active'));
        el.classList.add('active');

        selectedKeys[gi] = label;

        // Swap main image to the variation's image when the swatch carries one
        const varImg = el.getAttribute('data-qa-image');
        if (varImg) {
            mainImg.style.opacity = '0';
            setTimeout(() => { mainImg.src = varImg; mainImg.style.opacity = '1'; }, 180);
            // Sync thumbnail highlight
            thumbsWrap.querySelectorAll('.qa-thumb').forEach(t => {
                t.classList.toggle('active', t.getAttribute('data-qa-src') === varImg);
            });
        }

        updateVariationUI();
    }

    /* ══════════════════════════════════════════════════
       BUILD VARIATION DOM
    ══════════════════════════════════════════════════ */
    function buildVariations(variations) {
        varWrap.innerHTML     = '';
        varNote.style.display = 'none';

        variations.forEach((grp, gi) => {
            const gEl = document.createElement('div');
            gEl.className = 'qa-var-group';
            gEl.setAttribute('data-qa-gi', gi);

            const lEl = document.createElement('div');
            lEl.className   = 'qa-var-label';
            lEl.textContent = grp.name;
            gEl.appendChild(lEl);

            const oEl = document.createElement('div');
            oEl.className = 'qa-var-options';

            (grp.values || []).forEach(val => {
                if (val.imagePath) {
                    const sw = document.createElement('div');
                    sw.className = 'qa-var-swatch';
                    sw.setAttribute('data-qa-gi',    gi);
                    sw.setAttribute('data-qa-label', val.label);
                    sw.setAttribute('data-qa-image', val.imagePath);
                    sw.setAttribute('tabindex',      '0');
                    sw.setAttribute('title',         val.label);

                    const img = document.createElement('img');
                    img.src = val.imagePath;
                    img.alt = val.label;

                    const span = document.createElement('span');
                    span.className   = 'qa-swatch-label';
                    span.textContent = val.label;

                    sw.appendChild(img);
                    sw.appendChild(span);

                    sw.addEventListener('click',   ()  => handleVarClick(sw));
                    sw.addEventListener('keydown', e => {
                        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleVarClick(sw); }
                    });
                    oEl.appendChild(sw);
                } else {
                    /* Text-only button */
                    const btn = document.createElement('button');
                    btn.type      = 'button';
                    btn.className = 'qa-var-btn';
                    btn.setAttribute('data-qa-gi',    gi);
                    btn.setAttribute('data-qa-label', val.label);
                    btn.textContent = val.label;
                    btn.addEventListener('click', () => handleVarClick(btn));
                    oEl.appendChild(btn);
                }
            });

            gEl.appendChild(oEl);
            varWrap.appendChild(gEl);
        });
    }

    /* ══════════════════════════════════════════════════
       POPULATE WITH API DATA
    ══════════════════════════════════════════════════ */
    function populate(p) {
        allCombos    = JSON.parse(p.variationCombosJson || '[]');
        groupNames   = (p.variations || []).map(v => v.name);
        selectedKeys = new Array(groupNames.length).fill(null);

        /* Images */
        const imgs = (p.images && p.images.length) ? p.images : [];
        mainImg.src = imgs[0] || '/images/placeholder.png';
        mainImg.alt = p.name;

        thumbsWrap.innerHTML = '';
        if (imgs.length > 1) {
            imgs.forEach((src, i) => {
                const d = document.createElement('div');
                d.className = 'qa-thumb' + (i === 0 ? ' active' : '');
                d.setAttribute('data-qa-src', src);
                d.innerHTML = '<img src="' + src + '" alt="' + p.name + ' view ' + (i + 1) + '" />';
                d.addEventListener('click', () => {
                    mainImg.style.opacity = '0';
                    setTimeout(() => { mainImg.src = src; mainImg.style.opacity = '1'; }, 180);
                    thumbsWrap.querySelectorAll('.qa-thumb').forEach(t => t.classList.remove('active'));
                    d.classList.add('active');
                });
                thumbsWrap.appendChild(d);
            });
        }

        /* Text fields */
        shopEl.textContent  = 'Sold by ' + (p.shopName || 'Unknown Shop');
        nameEl.textContent  = p.name;

        const original = parseFloat(p.originalPrice || 0);
        const selling  = parseFloat(p.price || 0);

        priceEl.textContent = 'RM ' + selling.toFixed(2);

        originalPriceEl.style.display = 'none';
        discountEl.style.display = 'none';

        if (original > selling && original > 0) {

            const discount =
                Math.round(((original - selling) / original) * 100);

            originalPriceEl.style.display = 'block';
            originalPriceEl.textContent = 'RM ' + original.toFixed(2);

            discountEl.style.display = 'block';
            discountEl.textContent = '-' + discount + '%';
        } else {

            if (originalPriceEl) originalPriceEl.style.display = 'none';
            if (discountEl) discountEl.style.display = 'none';
        }
        
        descEl.textContent  = p.description || 'No description provided.';
        viewFullLink.href   = '/Customer/ProductDetails/' + p.productId;
        productIdEl.value   = p.productId;

        /* Variations or plain stock */
        if (groupNames.length > 0) {
            buildVariations(p.variations);
            setBadge('select-options', 'Select Options');
            submitBtn.disabled    = true;
            qtyMinus.disabled     = true;
            qtyPlus.disabled      = true;
            stockNote.textContent = '';
            updateOptionAvailability();
        } else {
            varWrap.innerHTML = '';
            const stock = p.stockQuantity || 0;
            if (stock <= 0)      setBadge('out-of-stock', 'Out of Stock');
            else if (stock <= 5) setBadge('low-stock',    'Low Stock');
            else                 setBadge('in-stock',      'In Stock');

            qtyInput.max          = Math.max(1, stock);
            setQty(1);
            stockNote.textContent = stock > 0 ? stock + ' left' : '';
            submitBtn.disabled    = stock <= 0;
            qtyMinus.disabled     = false;
            qtyPlus.disabled      = false;
            selVarsEl.value       = '{}';
        }
    }

    /* ══════════════════════════════════════════════════
       FORM VALIDATION
    ══════════════════════════════════════════════════ */
    form.addEventListener('submit', function (e) {
        if (groupNames.length > 0) {
            if (!selectedKeys.every(v => v !== null)) {
                e.preventDefault();
                toast('Please select all product variations.', 'error');
                return;
            }
            if (findComboStock() <= 0) {
                e.preventDefault();
                toast('This combination is out of stock.', 'error');
                return;
            }
        }
        updateHidden();
        setQty(parseInt(qtyInput.value, 10));
    });

    /* ══════════════════════════════════════════════════
       PUBLIC API — called by Home.cshtml
    ══════════════════════════════════════════════════ */
    window.openQuickAdd = async function (productId) {
        const ru = document.getElementById('qaReturnUrl');
        if (ru) ru.value = window.location.pathname + window.location.search;

        backdrop.classList.add('open');
        openPanel(skeleton);
        drawer.style.display = 'none';
        drawer.classList.remove('open');
        document.body.style.overflow = 'hidden';

        try {
            const res = await fetch('/Customer/QuickAddData?id=' + productId);
            if (!res.ok) throw new Error();
            const data = await res.json();

            populate(data);

            closePanel(skeleton);
            setTimeout(() => openPanel(drawer), 50);
        } catch {
            backdrop.classList.remove('open');
            closePanel(skeleton);
            document.body.style.overflow = '';
            toast('Could not load product details. Please try again.', 'error');
        }
    };
})();