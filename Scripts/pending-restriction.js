(function () {
    function isUserPending() {
        var body = document.body;
        if (!body || !body.dataset) return false;
        var userStatus = (body.dataset.userStatus || '').trim().toUpperCase();
        var verifStatus = (body.dataset.verificationStatus || '').trim().toUpperCase();
        return userStatus === 'PENDING' || verifStatus === 'PENDING';
    }

    window.isPending = isUserPending();

    window.showPendingModal = function (e) {
        if (isUserPending()) {
            if (e) {
                if (e.preventDefault) e.preventDefault();
                if (e.stopImmediatePropagation) e.stopImmediatePropagation();
                if (e.stopPropagation) e.stopPropagation();
            }
            var modal = document.getElementById('pendingModal');
            if (modal) {
                modal.style.display = 'flex';
            }
            return false;
        }
        return true;
    };

    function isRestrictedElement(element) {
        if (!element || !element.closest) return false;

        // 1. Send Message
        if (element.closest('#cr-send, .btn-send-message, #btnSendMessage, #cr-form button[type="submit"], .us-chat__form button[type="submit"]')) {
            return true;
        }

        // 2. Leave a Review / Rate
        if (element.closest('[data-act="rate"], .btn-leave-review, #btnLeaveReview, .btn-review, #bi-rate-form button[type="submit"]')) {
            return true;
        }

        // 3. Export Reports
        if (element.closest('#btnExportReport, .btn-export-report, #btnExport, [data-export-report], .reports-btn--secondary')) {
            return true;
        }

        // Check button/link text content fallback
        var btn = element.closest('button, a, input[type="submit"]');
        if (btn) {
            var text = (btn.textContent || btn.value || '').trim().toLowerCase();
            if (text === 'send' || text === 'send message' ||
                text === 'leave a review' || text === 'to rate' || text === 'submit rating' ||
                text === 'export' || text === 'export report' || text === 'download report') {
                return true;
            }
        }

        return false;
    }

    // Intercept clicks on restricted buttons via event delegation
    document.addEventListener('click', function (e) {
        if (!isUserPending()) return;

        if (isRestrictedElement(e.target)) {
            window.showPendingModal(e);
        }
    }, true);

    // Intercept form submissions on restricted forms
    document.addEventListener('submit', function (e) {
        if (!isUserPending()) return;

        var form = e.target;
        if (!form) return;

        if (form.matches && (form.matches('#cr-form, #bi-rate-form, .us-chat__form, #pm-chat-form') ||
            (form.querySelector && form.querySelector('#cr-send, .btn-send-message, #btnExportReport, .btn-export-report, [data-act="rate"], .btn-leave-review')))) {
            window.showPendingModal(e);
        }
    }, true);
})();
