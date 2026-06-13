(function () {
    // Profile dropdown
    const profileDropdown = document.querySelector('.profile-dropdown');
    const profileBtn = profileDropdown?.querySelector('.profile-btn');
    if (profileBtn) {
        profileBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            profileDropdown.classList.toggle('open');
        });
    }

    // Hamburger / mobile drawer
    const hamburgerBtn = document.getElementById('hamburgerBtn');
    const mobileDrawer = document.getElementById('mobileDrawer');
    if (hamburgerBtn && mobileDrawer) {
    hamburgerBtn.addEventListener('click', function (e) {
        e.stopPropagation();
        const isOpen = mobileDrawer.classList.toggle('open');
        hamburgerBtn.querySelector('i').className = isOpen
        ? 'ti ti-x'
        : 'ti ti-menu-2';
    });
    }

    // Close both on outside click
    document.addEventListener('click', function () {
    profileDropdown?.classList.remove('open');
    mobileDrawer?.classList.remove('open');
    if (hamburgerBtn) {
        hamburgerBtn.querySelector('i').className = 'ti ti-menu-2';
    }
    });
})();