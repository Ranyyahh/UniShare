document.addEventListener('DOMContentLoaded', function() {
    const isPending = document.body.dataset.userStatus === 'PENDING' || document.body.dataset.verificationStatus === 'PENDING';
    
    if (!isPending) return; // If not pending, allow everything

    // Modal Logic
    window.showPendingModal = function(e) {
        if (e) {
            e.preventDefault();
            e.stopPropagation();
        }
        const modal = document.getElementById('pendingModal');
        if (modal) modal.style.display = 'flex';
    };

    // Intercept Clicks (Event Delegation)
    document.body.addEventListener('click', function(e) {
        // Patch: Block "Message Owner" in Item Details
        var clickedBtn = e.target.closest('button, a');
        if (clickedBtn) {
            var btnText = (clickedBtn.innerText || "").toLowerCase();
            var btnId = (clickedBtn.id || "").toLowerCase();
            var btnClass = (clickedBtn.className || "").toLowerCase();

            // Check if it's the Message Owner button
            if (btnText.includes('message owner') || 
                btnText.includes('contact owner') ||
                btnId.includes('messageowner') || 
                btnId === 'd-msg' ||
                clickedBtn.hasAttribute('data-msg-lender') ||
                btnClass.includes('message-owner') || 
                btnClass.includes('contact-owner')) {
                
                showPendingModal(e);
                return;
            }
        }

        const target = e.target.closest('a, button');
        if (!target) return;

        const text = target.innerText || "";
        const href = target.getAttribute('href') || "";
        const id = target.id || "";

        // 1. Block Navbar Links
        if (text.includes('My Listings') || 
            text.includes('My Rentals') || 
            text.includes('Borrowed Items') || 
            text.includes('Reports & Analytics') ||
            text.includes('Account Settings')) {
            showPendingModal(e);
            return;
        }

        // 2. Block Message Icon
        if (href.includes('/Messages') || target.classList.contains('message-button')) {
            showPendingModal(e);
            return;
        }

        // 3. Block Dashboard "List an Item" Button
        if (text.includes('List an Item') || id === 'btnListAnItem') {
            showPendingModal(e);
            return;
        }
    }, true); // Use capture phase to intercept before the actual click
});
