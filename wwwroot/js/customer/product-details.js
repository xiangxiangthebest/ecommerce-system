(function () {
    function qs(sel, root) { return (root || document).querySelector(sel); }
    function qsa(sel, root) { return Array.from((root || document).querySelectorAll(sel)); }

    /* ── Toast ── */
    function showToast(msg, type) {
        var t = document.createElement("div");
        t.className = "floating-alert" + (type === "error" ? " error" : "");
        t.textContent = msg;
        document.body.appendChild(t);
        setTimeout(function () {
        t.classList.add("hide");
        setTimeout(function () { t.remove(); }, 500);
        }, 3000);
    }

    /* ── Auto-hide server alert ── */
    document.addEventListener("DOMContentLoaded", function () {
        var a = qs("#floatingAlert");
        if (!a) return;
        setTimeout(function () {
        a.classList.add("hide");
        setTimeout(function () { a.remove(); }, 500);
        }, 3000);
    });

    /* ══════════════════════════════════════════════════
        GALLERY THUMBNAILS
    ══════════════════════════════════════════════════ */
    qsa(".pd-thumb").forEach(function (thumb) {
        thumb.addEventListener("click", function () {
        qs("#pdMainImg").src = thumb.getAttribute("data-src");
        qsa(".pd-thumb").forEach(function (t) { t.classList.remove("active"); });
        thumb.classList.add("active");
        });
    });

    /* ══════════════════════════════════════════════════
        GALLERY IMAGE LIGHTBOX (click main image to enlarge)
    ══════════════════════════════════════════════════ */
    var pdLightbox = qs("#pdImgLightbox");
    var pdLightboxImg = qs("#pdImgLightboxImg");
    var pdLightboxClose = qs("#pdImgLightboxClose");
    var pdMainImgWrap = qs("#pdMainImgWrap");

    function openGalleryLightbox() {
        pdLightboxImg.src = qs("#pdMainImg").src;
        pdLightbox.hidden = false;
        requestAnimationFrame(function () { pdLightbox.classList.add("open"); });
        document.body.style.overflow = "hidden";
    }
    function closeGalleryLightbox() {
        pdLightbox.classList.remove("open");
        pdLightbox.addEventListener("transitionend", function () {
        pdLightbox.hidden = true;
        document.body.style.overflow = "";
        }, { once: true });
    }

    if (pdMainImgWrap) {
        pdMainImgWrap.addEventListener("click", openGalleryLightbox);
    }
    if (pdLightboxClose) {
        pdLightboxClose.addEventListener("click", function (e) {
        e.stopPropagation();
        closeGalleryLightbox();
        });
    }
    if (pdLightbox) {
        pdLightbox.addEventListener("click", function (e) {
        if (e.target === pdLightbox) closeGalleryLightbox();
        });
    }
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && pdLightbox && !pdLightbox.hidden) closeGalleryLightbox();
    });

    /* ══════════════════════════════════════════════════
        QUANTITY
    ══════════════════════════════════════════════════ */
    var qtyInput = qs("#pdQty");
    var qtyHidden = qs("#cartQuantity");
    var btnMinus = qs("#pdQtyMinus");
    var btnPlus = qs("#pdQtyPlus");

    function clampQty(v) {
        return Math.min(Math.max(1, v), Math.max(1, parseInt(qtyInput.max || "1", 10)));
    }
    function setQty(v) {
        var c = clampQty(v);
        qtyInput.value = c;
        if (qtyHidden) qtyHidden.value = c;
    }
    if (btnMinus) btnMinus.addEventListener("click", function () {
        setQty(parseInt(qtyInput.value || "1", 10) - 1);
    });
    if (btnPlus) btnPlus.addEventListener("click", function () {
        setQty(parseInt(qtyInput.value || "1", 10) + 1);
    });

    /* ══════════════════════════════════════════════════
        VARIATIONS
    ══════════════════════════════════════════════════ */

    // Read server-provided config
    var cfg = window.__PD || {};
    var allCombos = cfg.allCombos || [];
    var groupNames = cfg.groupNames || [];
    var totalGroups = groupNames.length;
    var selectedKeys = new Array(totalGroups).fill(null);

    var badge = qs("#pdStockBadge");
    var stockNote = qs("#pdVariationStockNote");
    var btnCart = qs("#pdBtnCart");
    var btnBuy = qs("#pdBtnBuy");
    var hiddenSel = qs("#selectedVariations");

    function setBadge(state, text) {
        if (!badge) return;
        badge.className = "pd-badge " + state;
        badge.textContent = text;
    }

    function updateHiddenSelected() {
        if (!hiddenSel) return;
        var obj = {};
        for (var i = 0; i < totalGroups; i++) {
        if (selectedKeys[i] !== null) obj[groupNames[i]] = selectedKeys[i];
        }
        hiddenSel.value = JSON.stringify(obj);
    }

    function findMatchingComboStock() {
        var m = allCombos.find(function (c) {
        return Array.isArray(c.keys) && c.keys.length === totalGroups &&
            selectedKeys.every(function (k, i) { return c.keys[i] === k; });
        });
        return m ? (m.stock || 0) : 0;
    }

    function comboHasStockForPartial(partial) {
        return allCombos.some(function (c) {
        if (!Array.isArray(c.keys) || c.keys.length !== totalGroups) return false;
        for (var i = 0; i < totalGroups; i++) {
            if (partial[i] !== null && c.keys[i] !== partial[i]) return false;
        }
        return (c.stock || 0) > 0;
        });
    }

    function setButtonsEnabled(enabled) {
        if (btnCart) btnCart.disabled = !enabled;
        if (btnBuy) btnBuy.disabled = !enabled;
        if (btnMinus) btnMinus.disabled = !enabled;
        if (btnPlus) btnPlus.disabled = !enabled;
    }

    function updateOptionAvailability() {
        for (var gi = 0; gi < totalGroups; gi++) {
        var groupEl = qs('.pd-variation-group[data-group-index="' + gi + '"]');
        if (!groupEl) continue;

        qsa("[data-label]", groupEl).forEach(function (opt) {
            var trial = selectedKeys.slice();
            trial[gi] = opt.getAttribute("data-label");
            var avail = comboHasStockForPartial(trial);

            opt.classList.toggle("unavailable", !avail);

            if (opt.tagName === "BUTTON") {
            opt.disabled = !avail;
            } else {
            opt.setAttribute("aria-disabled", String(!avail));
            opt.style.pointerEvents = avail ? "auto" : "none";
            }
        });
        }
    }

    function updateVariationUI() {
        var allSelected = selectedKeys.every(function (v) { return v !== null; });

        updateHiddenSelected();
        updateOptionAvailability();

        if (!allSelected) {
        setBadge("select-options", "Select Options");
        if (stockNote) stockNote.style.display = "none";
        setButtonsEnabled(false);
        return;
        }

        var stock = findMatchingComboStock();
        if (stock <= 0) setBadge("out-of-stock", "Out of Stock");
        else if (stock <= 5) setBadge("low-stock", "Low Stock");
        else setBadge("in-stock", "In Stock");

        if (stockNote) {
        stockNote.style.display = "block";
        stockNote.textContent = stock > 0
            ? (stock + " left for this option")
            : "This combination is out of stock";
        }

        if (qtyInput) {
        qtyInput.max = Math.max(1, stock);
        setQty(parseInt(qtyInput.value || "1", 10));
        }

        setButtonsEnabled(stock > 0);
    }

    function handleVariationClick(el) {
        var gi = parseInt(el.getAttribute("data-group-index"), 10);
        var label = el.getAttribute("data-label");
        if (isNaN(gi) || !label) return;

        var groupEl = el.closest(".pd-variation-group");
        if (!groupEl) return;

        qsa(".pd-variation-btn, .pd-variation-swatch", groupEl)
        .forEach(function (x) { x.classList.remove("active"); });

        el.classList.add("active");
        selectedKeys[gi] = label;

        // Update the selected-label span next to the group heading
        var selLabel = qs("#pdSelLabel-" + gi);
        if (selLabel) selLabel.textContent = ": " + label;

        // Switch main image to the variation's image when available
        var varImg = el.getAttribute("data-image");
        if (varImg) {
        var mainImg = qs("#pdMainImg");
        if (mainImg) mainImg.src = varImg;

        qsa(".pd-thumb").forEach(function (t) { t.classList.remove("active"); });
        var matchThumb = qsa(".pd-thumb").find(function (t) {
            return t.getAttribute("data-src") === varImg;
        });
        if (matchThumb) matchThumb.classList.add("active");
        }

        updateVariationUI();
    }

    qsa(".pd-variation-btn, .pd-variation-swatch").forEach(function (el) {
        el.addEventListener("click", function () { handleVariationClick(el); });
        el.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            handleVariationClick(el);
        }
        });
    });

    if (totalGroups > 0) {
        setButtonsEnabled(false);
        updateOptionAvailability();
    }

    window.validateVariationsBeforeSubmit = function () {
        if (totalGroups <= 0) return true;

        if (!selectedKeys.every(function (v) { return v !== null; })) {
        showToast("Please select all product variations before adding to cart.", "error");
        return false;
        }
        if (findMatchingComboStock() <= 0) {
        showToast("This variation combination is out of stock.", "error");
        return false;
        }

        updateHiddenSelected();
        if (qtyInput) setQty(parseInt(qtyInput.value || "1", 10));
        return true;
    };

    window.prepareBuyNow = function () {
        if (!window.validateVariationsBeforeSubmit()) return false;
        var bnQty = qs("#buyNowQuantity");
        var bnSel = qs("#buyNowSelectedVariations");
        if (bnQty) bnQty.value = qs("#pdQty").value;
        if (bnSel) bnSel.value = qs("#selectedVariations").value;
        return true;
    };

    /* ══════════════════════════════════════════════════
        REVIEW IMAGE LIGHTBOX
    ══════════════════════════════════════════════════ */
    window.rvOpenLightbox = function (src) {
        var box = document.getElementById("rvLightbox");
        var img = document.getElementById("rvLightboxImg");
        if (img) img.src = src;
        if (box) box.hidden = false;
        document.body.style.overflow = "hidden";
    };

    window.rvCloseLightbox = function () {
        var box = document.getElementById("rvLightbox");
        var img = document.getElementById("rvLightboxImg");
        if (img) img.src = "";
        if (box) box.hidden = true;
        document.body.style.overflow = "";
    };

    /* ══════════════════════════════════════════════════
        REVIEWS MODAL
    ══════════════════════════════════════════════════ */
    var backdrop = qs("#rvBackdrop");
    var triggerBtn = qs("#reviewTriggerBtn");
    var closeBtn = qs("#rvCloseBtn");

    if (backdrop && triggerBtn) {
        function openModal() {
        backdrop.hidden = false;
        requestAnimationFrame(function () { backdrop.classList.add("open"); });
        triggerBtn.setAttribute("aria-expanded", "true");
        if (closeBtn) closeBtn.focus();
        document.body.style.overflow = "hidden";
        }
        function closeModal() {
        backdrop.classList.remove("open");
        backdrop.addEventListener("transitionend", function () {
            backdrop.hidden = true;
            document.body.style.overflow = "";
            triggerBtn.setAttribute("aria-expanded", "false");
            triggerBtn.focus();
        }, { once: true });
        }

        triggerBtn.addEventListener("click", openModal);
        triggerBtn.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); openModal(); }
        });
        if (closeBtn) closeBtn.addEventListener("click", closeModal);
        backdrop.addEventListener("click", function (e) { if (e.target === backdrop) closeModal(); });
        document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && !backdrop.hidden) closeModal();
        });

        /* Filter chips */
        var cards = qsa(".rv-card");
        var empty = qs("#rvEmpty");
        var chips = qsa(".rv-chip");

        chips.forEach(function (chip) {
        chip.addEventListener("click", function () {
            chips.forEach(function (c) { c.classList.remove("active"); });
            chip.classList.add("active");

            var filter = chip.dataset.filter;
            var visible = 0;

            cards.forEach(function (card) {
            var show = filter === "all" || card.dataset.rating === filter;
            card.style.display = show ? "" : "none";
            if (show) visible++;
            });

            if (empty) empty.style.display = visible === 0 ? "flex" : "none";

            var body = qs("#rvBody");
            if (body) body.scrollTop = 0;
        });
        });
    }
})();