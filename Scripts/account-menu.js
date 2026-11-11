(function () {
    'use strict';

    var menu = document.querySelector('[data-account-menu]');
    var modal = document.querySelector('[data-account-modal]');
    if (!menu || !modal) return;

    var toggle = menu.querySelector('[data-account-toggle]');
    var dropdown = menu.querySelector('.account-dropdown');
    var openModalButton = menu.querySelector('[data-account-open]');
    var dialog = modal.querySelector('[data-account-dialog]');
    var profile = modal.querySelector('[data-account-profile]');
    var previousFocus = null;
    var requestId = 0;

    function setDropdown(open) {
        dropdown.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
    }

    function textElement(tagName, className, value) {
        var element = document.createElement(tagName);
        if (className) element.className = className;
        element.textContent = value;
        return element;
    }

    function field(label, value) {
        var wrapper = document.createElement('div');
        wrapper.className = 'account-profile-field';
        wrapper.appendChild(textElement('dt', '', label));
        wrapper.appendChild(textElement('dd', '', value || 'Not available'));
        return wrapper;
    }

    function showProfile(data) {
        profile.textContent = '';
        var name = data && data.displayName ? data.displayName : 'Account';
        var summary = document.createElement('div');
        summary.className = 'account-profile-summary';
        var avatar = textElement('span', 'account-avatar account-avatar--large', '?');
        avatar.setAttribute('aria-hidden', 'true');
        var initials = name.split(/\s+/).filter(Boolean).slice(0, 2).map(function (part) { return part.charAt(0); }).join('');
        avatar.textContent = initials || '?';
        summary.appendChild(avatar);
        var copy = document.createElement('div');
        copy.appendChild(textElement('h3', '', name));
        var subtitle = (data && data.role === 'ADMIN' ? 'UniShare Administrator' : 'QCU Student &bull; Peer Rental Member');
        var subP = document.createElement('p');
        subP.innerHTML = subtitle;
        copy.appendChild(subP);
        summary.appendChild(copy);
        profile.appendChild(summary);

        var grid = document.createElement('dl');
        grid.className = 'account-profile-grid';
        grid.appendChild(field('Student ID', data ? data.studentId : null));
        grid.appendChild(field('Email address', data ? data.email : null));
        grid.appendChild(field('Course / program', data ? data.program : null));
        grid.appendChild(field('Year level', data ? data.yearLevel : null));
        grid.appendChild(field('Section', data ? data.section : null));
        grid.appendChild(field('Contact', data ? data.phone : null));
        grid.appendChild(field('Verification status', data ? data.verificationStatus : null));
        grid.appendChild(field('Role', data ? data.role : null));
        grid.appendChild(field('Member since', data ? data.registered : null));
        profile.appendChild(grid);
    }

    function loadProfile() {
        var currentRequest = ++requestId;
        profile.textContent = '';
        var loading = document.createElement('div');
        loading.className = 'account-profile-loading';
        loading.setAttribute('role', 'status');
        loading.appendChild(textElement('span', '', 'Loading profile…'));
        profile.appendChild(loading);

        fetch(modal.getAttribute('data-profile-url'), {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        }).then(function (response) {
            if (response.status === 401) throw new Error('Sign in to view your account profile.');
            if (!response.ok) throw new Error('Profile information is currently unavailable.');
            return response.json();
        }).then(function (data) {
            if (currentRequest === requestId && !modal.hidden) showProfile(data);
        }).catch(function (error) {
            if (currentRequest !== requestId || modal.hidden) return;
            profile.textContent = '';
            var message = document.createElement('p');
            message.className = 'account-profile-error';
            message.setAttribute('role', 'status');
            message.textContent = error.message;
            profile.appendChild(message);
        });
    }

    function openModal() {
        setDropdown(false);
        previousFocus = document.activeElement;
        modal.hidden = false;
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('account-modal-open');
        dialog.focus();
        loadProfile();
    }

    function closeModal() {
        if (modal.hidden) return;
        modal.hidden = true;
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('account-modal-open');
        requestId++;
        if (previousFocus && typeof previousFocus.focus === 'function') previousFocus.focus();
    }

    toggle.addEventListener('click', function () {
        setDropdown(dropdown.hidden);
    });

    openModalButton.addEventListener('click', openModal);

    document.addEventListener('click', function (event) {
        if (!menu.contains(event.target)) setDropdown(false);
        if (event.target.closest && event.target.closest('[data-account-close]')) closeModal();
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && !modal.hidden) {
            closeModal();
            return;
        }
        if (event.key === 'Escape' && !dropdown.hidden) {
            setDropdown(false);
            toggle.focus();
            return;
        }
        if (event.key === 'Tab' && !modal.hidden) {
            var focusable = dialog.querySelectorAll('button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])');
            if (!focusable.length) {
                event.preventDefault();
                dialog.focus();
                return;
            }
            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && (document.activeElement === first || document.activeElement === dialog)) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    });

    var headerName = document.querySelector('[data-account-name]');
    var headerAvatar = document.querySelector('[data-account-avatar]');
    var headerSubtitle = document.querySelector('[data-account-subtitle]');
    var profileUrl = modal.getAttribute('data-profile-url');
    fetch(profileUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (response) { return response.ok ? response.json() : null; })
        .then(function (data) {
            if (!data || !data.displayName) return;
            if (headerName) headerName.textContent = data.displayName;
            var initials = data.displayName.split(/\s+/).filter(Boolean).slice(0, 2).map(function (part) { return part.charAt(0); }).join('');
            if (headerAvatar) headerAvatar.textContent = initials || '?';
            if (headerSubtitle) {
                if (data.role === 'ADMIN') headerSubtitle.textContent = 'Administrator';
                else if (data.verificationStatus === 'APPROVED') headerSubtitle.textContent = 'Verified Student';
                else headerSubtitle.textContent = 'Pending Verification';
            }
        })
        .catch(function () { /* Keep the neutral account label when no profile is available. */ });
}());
