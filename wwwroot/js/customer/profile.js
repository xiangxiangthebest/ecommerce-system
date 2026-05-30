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

        document.querySelector('[name="Phone"]').value =
            sessionStorage.getItem("profilePhone") || "";

        // One-time restore only
        sessionStorage.removeItem("profileDraftRestore");
    }

    if (fullName)
        document.querySelector('[name="FullName"]').value = fullName;

    if (gender)
        document.querySelector('[name="Gender"]').value = gender;

    if (birthday)
        document.querySelector('[name="Birthday"]').value = birthday;

    if (phone)
        document.querySelector('[name="Phone"]').value = phone;

    document.getElementById("profileForm")?.addEventListener("submit", () => {
        sessionStorage.removeItem("profileFullName");
        sessionStorage.removeItem("profileGender");
        sessionStorage.removeItem("profileBirthday");
        sessionStorage.removeItem("profilePhone");
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
        document.querySelector('[name="Phone"]')?.value || "");
}

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