// ─── Tab switching ────────────────────────────────────
function switchTab(tabName) {
    document.querySelectorAll('.tab-content').forEach(tab => {
        tab.classList.remove('active');
    });

    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.remove('active');
    });

    document.getElementById(tabName).classList.add('active');

    const btnId = tabName === 'productReports' ? 'productReportsTab' : 'reviewReportsTab';
    document.getElementById(btnId).classList.add('active');
}

// ─── Image lightbox ───────────────────────────────────
function openImageLightbox(src) {
    const lightbox = document.getElementById('imageLightbox');
    const image    = document.getElementById('lightboxImage');
    image.src = src;
    lightbox.hidden = false;
    // Small delay so the hidden→visible transition fires
    requestAnimationFrame(() => lightbox.classList.add('open'));
    document.body.style.overflow = 'hidden';
}

function closeImageLightbox() {
    const lightbox = document.getElementById('imageLightbox');
    lightbox.classList.remove('open');
    // Wait for fade-out before hiding
    lightbox.addEventListener('transitionend', () => {
        lightbox.hidden = true;
        document.body.style.overflow = '';
    }, { once: true });
}

// ─── Update Report Status ──────────────────────────────
function updateReportStatus(reportId, reportType, newStatus) {
    if (!confirm(`Are you sure you want to change this report status to ${newStatus}?`)) {
        return;
    }

    fetch(`/Customer/UpdateReportStatus`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({
            reportId: reportId,
            reportType: reportType,
            newStatus: newStatus
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            alert(data.message);
            location.reload();
        } else {
            alert('Error: ' + data.message);
        }
    })
    .catch(error => {
        console.error('Error:', error);
        alert('An error occurred while updating the report status.');
    });
}

// ─── DOM ready ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    const lightbox = document.getElementById('imageLightbox');
    const closeBtn = document.getElementById('lightboxClose');

    if (!lightbox) return;

    closeBtn.addEventListener('click', closeImageLightbox);

    lightbox.addEventListener('click', function (e) {
        if (e.target === lightbox) closeImageLightbox();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !lightbox.hidden) closeImageLightbox();
    });
});
