/* UniShare - Messages and Notifications pop-up windows (navbar icons) */
(function () {
    'use strict';

    var dataEl = document.getElementById('pm-data');
    var notesDlg = document.getElementById('pm-notes');
    var msgsDlg = document.getElementById('pm-msgs');
    if (!dataEl || !notesDlg || !msgsDlg) { return; }

    var data = { conversations: [], notifications: [] };
    try { data = JSON.parse(dataEl.textContent); }
    catch (err) { if (window.console) { console.error('UniShare popups: could not read data', err); } }
    var convos = data.conversations || [];
    var notes = data.notifications || [];
    var activeId = null;

    function $(id) { return document.getElementById(id); }
    function esc(v) {
        return String(v === null || v === undefined ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function lower(s) { return String(s).toLowerCase(); }
    function open(dlg) { if (typeof dlg.showModal === 'function' && !dlg.open) { dlg.showModal(); } }
    function setTriggerState(selector, on) {
        [].slice.call(document.querySelectorAll(selector)).forEach(function (el) { el.classList.toggle('is-open', on); });
    }

    /* ------------------------------------------------------------ notifications */
    function unreadNotes() { return notes.filter(function (n) { return n.unread; }).length; }

    function updateBell() {
        var n = unreadNotes();
        [].slice.call(document.querySelectorAll('.notification-dot')).forEach(function (dot) { dot.style.display = n ? '' : 'none'; });
        var count = $('pm-notes-count');
        count.hidden = n === 0;
        count.textContent = n + ' new';
        $('pm-notes-readall').disabled = n === 0;
    }

    function renderNotes() {
        var list = $('pm-notes-list');
        if (!notes.length) { list.innerHTML = '<li class="pm-empty">You have no notifications.</li>'; }
        else {
            list.innerHTML = notes.map(function (n) {
                return '<li class="' + (n.unread ? 'is-unread' : '') + '" data-note="' + n.id + '" tabindex="0">' +
                       '<div><strong>' + esc(n.title) + '</strong><p>' + esc(n.body) + '</p></div><small>' + esc(n.time) + '</small></li>';
            }).join('');
        }
        updateBell();
    }

    function markRead(id) {
        notes.forEach(function (n) { if (n.id === id) { n.unread = false; } });
        renderNotes();
    }

    $('pm-notes-readall').addEventListener('click', function () {
        notes.forEach(function (n) { n.unread = false; });
        renderNotes();
    });
    $('pm-notes-list').addEventListener('click', function (e) {
        var li = e.target.closest('[data-note]');
        if (li) { markRead(parseInt(li.getAttribute('data-note'), 10)); }
    });
    $('pm-notes-list').addEventListener('keydown', function (e) {
        var li = e.target.closest('[data-note]');
        if (li && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); markRead(parseInt(li.getAttribute('data-note'), 10)); }
    });

    function openNotes() { renderNotes(); setTriggerState('.notification-button', true); open(notesDlg); }

    /* ----------------------------------------------------------------- messages */
    function byId(id) { for (var i = 0; i < convos.length; i++) { if (convos[i].id === id) { return convos[i]; } } return null; }
    function initialsOf(name) {
        return String(name).split(/\s+/).filter(Boolean).slice(0, 2).map(function (p) { return p.charAt(0).toUpperCase(); }).join('');
    }
    function nowLabel() { return new Date().toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }); }

    function renderList() {
        $('pm-chat-list').innerHTML = convos.length ? convos.map(function (c) {
            return '<button type="button" class="us-chat__row' + (c.id === activeId ? ' is-active' : '') + '" data-convo="' + c.id + '">' +
                   '<span class="us-avatar">' + esc(c.initials) + '</span>' +
                   '<span class="us-chat__meta"><strong>' + esc(c.person) + (c.unread ? ' <i class="us-dot"></i>' : '') + '</strong>' +
                   '<small>' + esc(c.item) + '</small><em>' + esc(c.last || 'No messages yet') + '</em></span>' +
                   '<small>' + esc(c.time) + '</small></button>';
        }).join('') : '<p class="pm-empty">No conversations yet.</p>';
    }

    function renderChat(keepInput) {
        var c = byId(activeId);
        var panel = $('pm-chat-panel');
        if (!c) { panel.innerHTML = '<div class="pm-empty">Select a conversation.</div>'; return; }
        var typed = keepInput && panel.querySelector('input[name="text"]') ? panel.querySelector('input[name="text"]').value : '';
        panel.innerHTML = '<header><strong>' + esc(c.person) + '</strong>' + (c.item ? '<span class="us-muted"> &bull; ' + esc(c.item) + '</span>' : '') + '</header>' +
            '<div class="us-chat__body" id="pm-chat-body">' +
            (c.messages.length ? c.messages.map(function (m) {
                return '<div class="us-bubble' + (m.mine ? ' is-mine' : '') + '"><p>' + esc(m.text) + '</p><small>' + esc(m.time) + '</small></div>';
            }).join('') : '<p class="pm-empty">Say hello to ' + esc(c.person) + '.</p>') + '</div>' +
            '<form class="us-chat__form" id="pm-chat-form"><input type="text" name="text" placeholder="Type a message..." autocomplete="off" required />' +
            '<button type="submit" class="us-btn us-btn--primary">Send</button></form>';
        var input = panel.querySelector('input[name="text"]');
        input.value = typed;
        var body = $('pm-chat-body');
        body.scrollTop = body.scrollHeight;
    }

    function selectConvo(id) {
        activeId = id;
        var c = byId(id);
        if (c) { c.unread = false; }
        renderList();
        renderChat(false);
    }

    function openMsgs(person, item) {
        if (person) {
            var found = null;
            convos.forEach(function (c) { if (lower(c.person) === lower(person)) { found = c; } });
            if (!found) {
                found = { id: Date.now(), person: person, initials: initialsOf(person), item: item || '', last: '', time: '', unread: false, messages: [] };
                convos.unshift(found);
            }
            activeId = found.id;
        } else if (activeId === null && convos.length) {
            activeId = convos[0].id;
        }
        selectConvo(activeId);
        setTriggerState('.message-button', true);
        open(msgsDlg);
        var input = $('pm-chat-panel').querySelector('input[name="text"]');
        if (input) { input.focus(); }
    }

    $('pm-chat-list').addEventListener('click', function (e) {
        var row = e.target.closest('[data-convo]');
        if (row) { selectConvo(parseInt(row.getAttribute('data-convo'), 10)); }
    });
    $('pm-chat-panel').addEventListener('submit', function (e) {
        e.preventDefault();
        var c = byId(activeId);
        var input = e.target.querySelector('input[name="text"]');
        var text = input.value.trim();
        if (!c || !text) { return; }
        c.messages.push({ mine: true, text: text, time: nowLabel() });
        c.last = text;
        c.time = 'Just now';
        renderList();
        renderChat(false);
        $('pm-chat-panel').querySelector('input[name="text"]').focus();
    });

    /* ------------------------------------------------------------ open / close */
    document.addEventListener('click', function (e) {
        var msgBtn = e.target.closest('.message-button');
        if (msgBtn) { e.preventDefault(); openMsgs(); return; }
        var bell = e.target.closest('.notification-button');
        if (bell) { e.preventDefault(); openNotes(); return; }
        var lender = e.target.closest('[data-msg-lender]');
        if (lender) { e.preventDefault(); openMsgs(lender.getAttribute('data-msg-lender'), lender.getAttribute('data-msg-item')); }
    });

    [notesDlg, msgsDlg].forEach(function (d) {
        d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-pm-close]')) { d.close(); } });
    });
    notesDlg.addEventListener('close', function () { setTriggerState('.notification-button', false); });
    msgsDlg.addEventListener('close', function () { setTriggerState('.message-button', false); });

    updateBell();
})();
