document.addEventListener("DOMContentLoaded", () => {
    // Auto submit on profile image change
    const profileImageInput = document.getElementById("profileImageInput");
    if (profileImageInput) {
        profileImageInput.addEventListener("change", function () {
            if (this.files && this.files.length > 0) {
                document.getElementById("profileForm")?.submit();
            }
        });
    }

    // Floating alerts
    document.querySelectorAll(".js-floating-alert").forEach(alertBox => {
        setTimeout(() => {
            alertBox.classList.add("hide");
            setTimeout(() => alertBox.remove(), 500);
        }, 3000);
    });

    document.querySelectorAll('input[name="City"]').forEach(input => {
        input.addEventListener("input", function () {
            this.value = this.value.replace(/[^A-Za-z\s]/g, '');
        });
    });

    document.querySelectorAll('input[name="Postcode"]').forEach(input => {
        input.addEventListener("input", function () {
            this.value = this.value.replace(/[^0-9]/g, '');
        });
    });

    // Address UI
    const slot          = document.getElementById("addressInlineSlot");
    const showBtn       = document.getElementById("showAddressFormBtn");
    const addrForm      = document.getElementById("addAddressForm");
    const editForm      = document.getElementById("editAddressForm");
    const cancelBtn     = document.getElementById("cancelAddressBtn");
    const cancelEditBtn = document.getElementById("cancelEditAddressBtn");

    let currentPill = null;

    function closeAddForm() {
        if (!addrForm) return;
        addrForm.style.display = "none";
        if (showBtn) showBtn.style.display = "";
    }

    function closeEditForm() {
        if (!editForm) return;
        editForm.style.display = "none";
        if (currentPill) {
            currentPill.style.display = "";
            currentPill = null;
        }
    }

    // + Add New Address
    if (showBtn && addrForm && slot) {
        showBtn.addEventListener("click", () => {
            closeEditForm();
            slot.appendChild(addrForm);
            addrForm.style.display = "block";
            showBtn.style.display = "none";
            addrForm.scrollIntoView({ behavior: "smooth", block: "nearest" });
        });
    }

    if (cancelBtn) {
        cancelBtn.addEventListener("click", () => closeAddForm());
    }

    // Edit inline (expand at pill position and hide the pill)
    document.querySelectorAll(".editAddressBtn").forEach(btn => {
        btn.addEventListener("click", () => {
            closeAddForm();

            const pill = btn.closest(".addr-pill");
            if (!pill) return;

            if (currentPill && currentPill !== pill) {
                currentPill.style.display = "";
            }
            currentPill = pill;

            document.getElementById("editAddressId").value = btn.dataset.addressid || "";
            document.getElementById("editRecipient").value = btn.dataset.recipient || "";
            document.getElementById("editPhone").value = btn.dataset.phone || "";
            document.getElementById("editLine1").value = btn.dataset.line1 || "";
            document.getElementById("editLine2").value = btn.dataset.line2 || "";
            document.getElementById("editCity").value = btn.dataset.city || "";
            document.getElementById("editPostcode").value = btn.dataset.postcode || "";
            document.getElementById("editState").value = btn.dataset.state || "";
            document.getElementById("editIsDefault").checked = (btn.dataset.default === "true");

            pill.after(editForm);
            pill.style.display = "none";

            editForm.style.display = "block";
            editForm.scrollIntoView({ behavior: "smooth", block: "nearest" });
        });
    });

    if (cancelEditBtn) {
        cancelEditBtn.addEventListener("click", () => closeEditForm());
    }

    // Restore unsaved profile values
    if (sessionStorage.getItem("profileDraftRestore") === "true") {
        
        document.querySelector('[name="FullName"]').value =
            sessionStorage.getItem("profileFullName") || "";

        document.querySelector('[name="Gender"]').value =
            sessionStorage.getItem("profileGender") || "";

        document.querySelector('[name="Birthday"]').value =
            sessionStorage.getItem("profileBirthday") || "";

        document.querySelector('[name="PhoneNumber"]').value =
            sessionStorage.getItem("profilePhone") || "";

        // One-time restore only
        sessionStorage.removeItem("profileDraftRestore");
    }

    document.getElementById("profileForm")?.addEventListener("submit", () => {
        sessionStorage.removeItem("profileFullName");
        sessionStorage.removeItem("profileGender");
        sessionStorage.removeItem("profileBirthday");
        sessionStorage.removeItem("profilePhone");
    });

    // Lock fields (One time update only)
    const genderWrap = document.querySelector("#genderField")?.closest(".select-wrap");
    const birthdayField = document.getElementById("birthdayField");

    if (window.genderLocked && genderWrap) {
        const value = document.getElementById("genderField")?.value;

        const input = document.createElement("input");
        input.value = value;
        input.readOnly = true;

        genderWrap.replaceWith(input);
    }

    if (window.birthdayLocked && birthdayField) {
        birthdayField.readOnly = true;
    }

    // Info tooltip
    document.querySelectorAll(".info-icon").forEach(icon => {
        const msg = icon.dataset.message;

        const tooltip = document.createElement("div");
        tooltip.className = "info-tooltip";
        tooltip.innerText = msg;

        icon.appendChild(tooltip);

        icon.addEventListener("mouseenter", () => {
            icon.classList.add("show");
        });

        icon.addEventListener("mouseleave", () => {
            icon.classList.remove("show");
        });
    });

    // Add Address submit
    document.getElementById("addAddressForm")?.addEventListener("submit", () => {
        saveProfileDraft();
    });

    // Edit Address submit
    document.getElementById("editAddressForm")?.addEventListener("submit", () => {
        saveProfileDraft();
    });

    // Remove Address submit
    document.querySelectorAll('form[asp-action="RemoveAddress"]').forEach(form => {
        form.addEventListener("submit", () => {
            saveProfileDraft();
        });
    });
});

function saveProfileDraft() {
    sessionStorage.setItem("profileDraftRestore", "true");

    sessionStorage.setItem("profileFullName",
        document.querySelector('[name="FullName"]')?.value || "");

    sessionStorage.setItem("profileGender",
        document.querySelector('[name="Gender"]')?.value || "");

    sessionStorage.setItem("profileBirthday",
        document.querySelector('[name="Birthday"]')?.value || "");

    sessionStorage.setItem("profilePhone",
        document.querySelector('[name="PhoneNumber"]')?.value || "");
}