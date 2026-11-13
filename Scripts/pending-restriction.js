var isPending = (document.body && document.body.dataset && (document.body.dataset.userStatus === 'PENDING' || document.body.dataset.verificationStatus === 'PENDING'));

function showPendingModal(e) {
    if (typeof isPending === 'undefined') {
        isPending = (document.body && document.body.dataset && (document.body.dataset.userStatus === 'PENDING' || document.body.dataset.verificationStatus === 'PENDING'));
    }
    if (isPending) {
        if (e) {
            e.preventDefault();
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
}
