(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var page = document.querySelector('.cr-page[data-conversations-url]');
        if (!page) return;

        var list = document.getElementById('cr-list');
        var history = document.getElementById('cr-history');
        var form = document.getElementById('cr-form');
        var input = document.getElementById('cr-input');
        var send = document.getElementById('cr-send');
        var selectedId = page.getAttribute('data-initial-id') || '';
        var selectedThread = null;
        var lastMessageId = '';
        var isSending = false;

        function prop(obj, upper, lower) { return obj ? (obj[upper] !== undefined ? obj[upper] : obj[lower]) : undefined; }
        function text(value) { return value == null ? '' : String(value); }
        function make(tag, className, value) {
            var node = document.createElement(tag);
            if (className) node.className = className;
            if (value != null) node.textContent = value;
            return node;
        }
        function api(url, options) {
            options = options || { credentials: 'same-origin' };
            if (!options.cache) options.cache = 'no-store';
            return fetch(url, options).then(function (response) {
                return response.json().catch(function () { return {}; }).then(function (data) {
                    if (!response.ok || !data.ok) throw new Error(data.message || 'Messages are unavailable.');
                    return data;
                });
            });
        }
        function toast(message, error) {
            var target = document.getElementById('cr-toast');
            if (!target) return;
            target.textContent = message || '';
            target.classList.toggle('is-error', !!error);
            target.classList.add('is-visible');
            window.setTimeout(function () { target.classList.remove('is-visible'); }, 3000);
        }
        function showEmpty(title, detail) {
            history.innerHTML = '';
            var empty = make('div', 'cr-no-results');
            empty.appendChild(make('b', '', title));
            empty.appendChild(make('small', '', detail));
            history.appendChild(empty);
            selectedThread = null;
            lastMessageId = '';
            send.disabled = true;
            input.disabled = true;
            input.placeholder = 'Select a conversation first...';
        }
        function setItem(thread) {
            var itemName = text(prop(thread, 'ItemName', 'itemName')) || 'Rental item';
            var image = text(prop(thread, 'ItemImage', 'itemImage'));
            var price = text(prop(thread, 'ItemPrice', 'itemPrice'));
            var imageBox = document.getElementById('cr-photo');
            var itemLink = document.getElementById('cr-item-link');
            document.getElementById('cr-detail-item').textContent = itemName;
            document.getElementById('cr-detail-price').textContent = price;
            document.getElementById('cr-detail-price').parentElement.style.display = price ? '' : 'none';
            imageBox.innerHTML = '';
            if (image) {
                var img = make('img', 'cr-item-image');
                img.src = image;
                img.alt = itemName;
                imageBox.appendChild(img);
            } else imageBox.appendChild(make('span', '', 'Item photo'));
            var itemUrl = text(prop(thread, 'ItemUrl', 'itemUrl'));
            itemLink.href = itemUrl || '#';
            itemLink.hidden = !itemUrl;
            document.getElementById('cr-status').textContent = 'About: ' + itemName;
        }
        function renderMessages(thread, forceScroll) {
            var messages = prop(thread, 'Messages', 'messages') || [];
            var newestId = messages.length ? text(prop(messages[messages.length - 1], 'Id', 'id')) : '';
            var nearBottom = history.scrollHeight - history.scrollTop - history.clientHeight < 80;
            var shouldScroll = forceScroll || newestId !== lastMessageId || nearBottom;
            history.innerHTML = '';
            if (!messages.length) {
                history.appendChild(make('div', 'cr-no-results', 'Start the conversation with a message about this item.'));
            } else {
                var currentDay = '';
                messages.forEach(function (message) {
                    var dayKey = text(prop(message, 'Day', 'day')) || new Date().toLocaleDateString();
                    if (dayKey !== currentDay) {
                        currentDay = dayKey;
                        var dateRow = make('div', 'cr-date');
                        dateRow.appendChild(make('span', '', dayKey));
                        history.appendChild(dateRow);
                    }
                    var mine = !!prop(message, 'Mine', 'mine');
                    var row = make('div', 'cr-message ' + (mine ? 'mine' : 'theirs'));
                    if (!mine) row.appendChild(make('span', 'cr-avatar lavender', text(prop(thread, 'Initials', 'initials'))));
                    var bubble = make('div');
                    bubble.appendChild(make('p', '', text(prop(message, 'Text', 'text'))));
                    var time = make('small');
                    time.appendChild(make('time', '', text(prop(message, 'Time', 'time'))));
                    bubble.appendChild(time);
                    row.appendChild(bubble);
                    history.appendChild(row);
                });
            }
            lastMessageId = newestId;
            if (shouldScroll) history.scrollTop = history.scrollHeight;
        }
        function loadThread(id, forceScroll) {
            if (!id) return Promise.resolve();
            var url = page.getAttribute('data-thread-url') + '?id=' + encodeURIComponent(id);
            return api(url).then(function (data) {
                if (selectedId !== id) return;
                var thread = data.thread;
                selectedThread = thread;
                document.getElementById('cr-person').textContent = text(prop(thread, 'With', 'with'));
                document.getElementById('cr-person-avatar').textContent = text(prop(thread, 'Initials', 'initials'));
                setItem(thread);
                renderMessages(thread, forceScroll);
                send.disabled = false;
                input.disabled = false;
                input.placeholder = 'Type a message...';
            });
        }
        function renderConversations(conversations) {
            list.innerHTML = '';
            conversations.forEach(function (conversation) {
                var id = text(prop(conversation, 'Id', 'id'));
                var button = make('button', 'cr-conversation' + (id === selectedId ? ' selected' : ''));
                button.type = 'button';
                button.setAttribute('data-id', id);
                button.setAttribute('data-unread', prop(conversation, 'Unread', 'unread') ? 'true' : 'false');
                button.appendChild(make('span', 'cr-avatar lavender', text(prop(conversation, 'Initials', 'initials'))));
                var copy = make('span', 'cr-convo-copy');
                copy.appendChild(make('b', '', text(prop(conversation, 'With', 'with'))));
                copy.appendChild(make('small', '', text(prop(conversation, 'LastMessage', 'lastMessage')) || 'No messages yet'));
                copy.appendChild(make('small', 'cr-item-preview', text(prop(conversation, 'ItemName', 'itemName'))));
                button.appendChild(copy);
                var meta = make('span', 'cr-meta');
                meta.appendChild(make('time', '', text(prop(conversation, 'Time', 'time'))));
                button.appendChild(meta);
                button.addEventListener('click', function () { selectConversation(id); });
                list.appendChild(button);
            });
            document.getElementById('cr-count').textContent = conversations.length;
            var title = document.getElementById('cr-empty-title');
            var copy = document.getElementById('cr-empty-copy');
            if (!conversations.length) {
                title.textContent = 'No conversations yet';
                copy.textContent = 'Open an item and choose Message Lender to start a conversation.';
                document.getElementById('cr-no-results').hidden = false;
                showEmpty('No conversation selected', 'Choose an item and contact its owner to start chatting.');
            } else {
                document.getElementById('cr-no-results').hidden = true;
                if (!conversations.some(function (c) { return text(prop(c, 'Id', 'id')) === selectedId; })) selectedId = text(prop(conversations[0], 'Id', 'id'));
                Array.prototype.forEach.call(list.children, function (button) {
                    button.classList.toggle('selected', button.getAttribute('data-id') === selectedId);
                });
            }
        }
        function loadConversations() {
            return api(page.getAttribute('data-conversations-url')).then(function (data) {
                var conversations = data.conversations || [];
                renderConversations(conversations);
                if (selectedId && conversations.some(function (c) { return text(prop(c, 'Id', 'id')) === selectedId; })) {
                    return loadThread(selectedId, false);
                }
                if (conversations.length && !selectedThread) return loadThread(selectedId, true);
            }).catch(function (error) {
                showEmpty('Messages unavailable', error.message);
            });
        }
        function selectConversation(id) {
            selectedId = id;
            lastMessageId = '';
            Array.prototype.forEach.call(list.children, function (button) {
                button.classList.toggle('selected', button.getAttribute('data-id') === selectedId);
            });
            loadThread(id, true).catch(function (error) { toast(error.message, true); });
        }

        document.getElementById('cr-search').addEventListener('input', function () {
            var query = this.value.toLowerCase().trim();
            var visible = 0;
            Array.prototype.forEach.call(list.children, function (button) {
                var matches = button.textContent.toLowerCase().indexOf(query) >= 0;
                button.classList.toggle('search-hidden', !matches);
                if (matches) visible++;
            });
            document.getElementById('cr-count').textContent = visible;
            document.getElementById('cr-no-results').hidden = visible !== 0;
            if (visible === 0 && list.children.length) {
                document.getElementById('cr-empty-title').textContent = 'No conversations found';
                document.getElementById('cr-empty-copy').textContent = 'Try searching another name or item.';
            }
            var container = document.querySelector('.cr-inbox-search');
            if (container) container.classList.toggle('has-value', !!query);
        });
        document.getElementById('cr-clear-search').addEventListener('click', function () {
            var search = document.getElementById('cr-search');
            search.value = '';
            search.dispatchEvent(new Event('input'));
            search.focus();
        });
        document.querySelectorAll('.cr-suggestions [data-message]').forEach(function (button) {
            button.addEventListener('click', function () {
                if (input.disabled) return;
                input.value = button.getAttribute('data-message');
                input.dispatchEvent(new Event('input'));
                input.focus();
            });
        });
        input.addEventListener('input', function () { send.disabled = !selectedId || !input.value.trim() || isSending; });
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            var body = new URLSearchParams();
            body.append('id', selectedId);
            body.append('text', input.value);
            var token = form.querySelector('input[name="__RequestVerificationToken"]');
            if (token) body.append('__RequestVerificationToken', token.value);
            if (!selectedId || !input.value.trim() || isSending) return;
            isSending = true;
            send.disabled = true;
            api(page.getAttribute('data-send-url'), {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' }, body: body.toString()
            }).then(function (data) {
                input.value = '';
                input.dispatchEvent(new Event('input'));
                return loadThread(selectedId, true).then(loadConversations);
            }).catch(function (error) { toast(error.message, true); })
              .then(function () { isSending = false; send.disabled = !selectedId || !input.value.trim(); });
        });
        document.getElementById('cr-back').addEventListener('click', function () {
            var inbox = document.querySelector('.cr-inbox');
            if (inbox) inbox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        });

        var notice = '@Html.Raw(System.Web.HttpUtility.JavaScriptStringEncode(Convert.ToString(ViewBag.ChatNotice)))';
        if (notice) toast(notice, true);
        loadConversations();
        window.setInterval(function () {
            if (selectedId) loadThread(selectedId, false).catch(function () { });
            loadConversations();
        }, 6000);
    });
})();
