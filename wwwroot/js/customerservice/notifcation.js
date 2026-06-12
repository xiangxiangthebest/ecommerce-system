function refreshAllCounts() {
    const totalItems  = document.querySelectorAll('.notif-item').length;
    const unreadItems = document.querySelectorAll('.notif-item[data-status="unread"]').length;

    // 1. Sidebar "All" pill
    const allPill = document.getElementById('filterCountAll');
    if (allPill) allPill.textContent = totalItems;

    // 2. Sidebar "Unread" pill
    const unreadPill = document.getElementById('filterCountUnread');
    if (unreadItems > 0) {
        if (unreadPill) {
            unreadPill.textContent = unreadItems;
        } else {
            // Create the pill if it didn't exist on page load (edge case: was 0 on load)
            const unreadBtn = document.querySelector('[data-filter="unread"]');
            if (unreadBtn) {
                const pill = document.createElement('span');
                pill.id = 'filterCountUnread';
                pill.className = 'notif-filter-count notif-filter-count--blue';
                pill.textContent = unreadItems;
                unreadBtn.appendChild(pill);
            }
        }
    } else {
        unreadPill?.remove();
    }

    // 3. Header "X unread" badge
    const headerBadge = document.getElementById('headerUnreadBadge');
    if (unreadItems > 0) {
        if (headerBadge) {
            headerBadge.textContent = unreadItems + ' unread';
        } else {
            const titleGroup = document.querySelector('.notif-main-title-group');
            if (titleGroup) {
                const badge = document.createElement('span');
                badge.id = 'headerUnreadBadge';
                badge.className = 'notif-unread-badge';
                badge.textContent = unreadItems + ' unread';
                titleGroup.appendChild(badge);
            }
        }
    } else {
        headerBadge?.remove();
        document.getElementById('markAllForm')?.remove();
    }

    // 4. Navbar bell badge
    const navBadge = document.querySelector('.notification-badge');
    if (unreadItems > 0) {
        if (navBadge) {
            navBadge.textContent = unreadItems > 99 ? '99+' : unreadItems;
        } else {
            const bellBtn = document.querySelector('.icon-btn');
            if (bellBtn) {
                const badge = document.createElement('span');
                badge.className = 'notification-badge';
                badge.textContent = unreadItems > 99 ? '99+' : unreadItems;
                bellBtn.appendChild(badge);
            }
        }
    } else {
        navBadge?.remove();
    }
}

// ─── Mark single as read ──────────────────────────────────────────────────
function markSingleRead(e, id) {
    e.preventDefault();
    const row   = document.getElementById('notif-' + id);
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    fetch(`/Notifications/MarkRead?id=${id}`, {
        method: 'POST',
        headers: { 'RequestVerificationToken': token }
    }).then(res => {
        if (!res.ok) return;

        row.classList.remove('is-unread');
        row.classList.add('is-read');
        row.dataset.status = 'read';
        row.querySelector('.notif-dot')?.remove();
        row.querySelector('.notif-item-action')?.remove();
        row.querySelector('.notif-item-icon-wrap')?.classList.remove('notif-item-icon-wrap--active');

        refreshAllCounts();
    });
}

// ─── Mark all as read ─────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {

    document.getElementById('markAllForm')?.addEventListener('submit', function (e) {
        e.preventDefault();
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

        fetch('/Notifications/CSMarkAllRead', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        }).then(res => {
            if (!res.ok) return;

            document.querySelectorAll('.notif-item.is-unread').forEach(row => {
                row.classList.remove('is-unread');
                row.classList.add('is-read');
                row.dataset.status = 'read';
                row.querySelector('.notif-dot')?.remove();
                row.querySelector('.notif-item-action')?.remove();
                row.querySelector('.notif-item-icon-wrap')?.classList.remove('notif-item-icon-wrap--active');
            });

            refreshAllCounts();
        });
    });

    // ─── Client-side filter ───────────────────────────────────────────────
    document.querySelectorAll('.notif-filter-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.notif-filter-btn').forEach(b => b.classList.remove('active'));
            this.classList.add('active');
            const filter = this.dataset.filter;
            document.querySelectorAll('.notif-item').forEach(item => {
                item.style.display = (filter === 'all' || item.dataset.status === filter) ? '' : 'none';
            });
        });
    });

});
