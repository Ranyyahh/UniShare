document.addEventListener('DOMContentLoaded', function() {
    const isPending = document.body.dataset.userStatus === 'PENDING' || document.body.dataset.verificationStatus === 'PENDING';
    
    if (!isPending) return; // Allow everything if not pending

    window.showPendingModal = function(e) {
        if (e) {
            e.preventDefault();
            if (e.stopImmediatePropagation) e.stopImmediatePropagation();
            e.stopPropagation();
        }
        const modal = document.getElementById('pendingModal');
        if (modal) modal.style.display = 'flex';
    };

    document.body.addEventListener('click', function(e) {
        const target = e.target.closest('button, a');
        if (!target) return;

        const text = (target.innerText || "").toLowerCase();
        const href = (target.getAttribute('href') || "").toLowerCase();
        const id = (target.id || "").toLowerCase();
        const className = (target.className || "").toLowerCase();

        // 1. Block Navbar Links & Account Settings & Message Icon
        if (text.includes('my listings') || 
            text.includes('my rentals') || 
            text.includes('borrowed items') || 
            text.includes('reports & analytics') ||
            text.includes('account settings') ||
            href.includes('/messages') || 
            href.includes('/account/settings') ||
            className.includes('message-button')) {
            showPendingModal(e);
            return;
        }

        // 2. Block "List an Item" button
        if (text.includes('list an item') || id.includes('btnlistanitem')) {
            showPendingModal(e);
            return;
        }

        // 3. Block "View Profile" and "Message Owner" (Item Details / Borrowed Items)
        if (text.includes('message owner') || 
            text.includes('contact owner') || 
            text.includes('message lender') || 
            target.hasAttribute('data-msg-lender') || 
            text.includes('view profile') || 
            id.includes('messageowner') || 
            id.includes('viewprofile') || 
            id === 'd-msg' || 
            className.includes('message-owner') || 
            className.includes('contact-owner') ||
            className.includes('view-profile') ||
            href.includes('/messages') || 
            href.includes('/profile') || 
            href.includes('/user/profile') ||
            href.includes('/users/profile') ||
            href.includes('/account/profile')) {
            showPendingModal(e);
            return;
        }

        // 4. Block "Borrow / Rent" buttons
        if (text.includes('borrow') || text.includes('rent') || text.includes('request') || id.includes('borrow') || id.includes('rent') || className.includes('btn-borrow') || className.includes('btn-rent') || className.includes('btn-request')) {
            showPendingModal(e);
            return;
        }

        // 5. Block "Leave a Review" button
        if (text.includes('leave a review') || text.includes('write a review') || id.includes('leavreview') || className.includes('leave-review') || className.includes('btn-review')) {
            showPendingModal(e);
            return;
        }
    }, true); 
});
