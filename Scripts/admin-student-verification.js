(function () {
    'use strict';
    document.addEventListener('DOMContentLoaded', function () {
        var page = document.querySelector('.asv-page');
        if (!page) return;
        var endpoint = page.dataset.endpoint, approveUrl = page.dataset.approveUrl, rejectUrl = page.dataset.rejectUrl;
        var search = document.getElementById('asvSearchInput'), status = document.getElementById('asvStatusSelect');
        var body = document.getElementById('asvTableBody'), pagination = page.querySelector('[data-asv-pagination]');
        var paginationInfo = page.querySelector('[data-asv-pagination-info]'), countLabel = page.querySelector('[data-asv-selected-count]');
        var feedback = page.querySelector('[data-asv-feedback]'), currentPage = 1, pageSize = 10, totalPages = 1, totalRows = 0, currentStudent = null, timer = null, requestSequence = 0;
        var overlay = document.getElementById('asvModalOverlay'), modal = document.getElementById('asvModalContainer');
        var statusConfig = { PENDING: ['Pending', 'asv-pill--pending'], APPROVED: ['Approved', 'asv-pill--approved'], REJECTED: ['Rejected', 'asv-pill--rejected'] };

        function text(node, value) { if (node) node.textContent = value == null || value === '' ? '—' : String(value); }
        function showFeedback(message, error) { if (!feedback) return; feedback.hidden = !message; feedback.textContent = message || ''; feedback.classList.toggle('is-error', !!error); }
        function setState(message) { body.innerHTML = ''; var row = document.createElement('tr'), cell = document.createElement('td'); cell.colSpan = 7; cell.className = 'asv-table__state'; cell.textContent = message; row.appendChild(cell); body.appendChild(row); }
        function updateStats(stats) {
            text(page.querySelector('[data-kpi-total]'), Number(stats.total || 0).toLocaleString());
            text(page.querySelector('[data-kpi-pending]'), Number(stats.pending || 0).toLocaleString());
            text(page.querySelector('[data-kpi-approved]'), Number(stats.approved || 0).toLocaleString());
            text(page.querySelector('[data-kpi-rejected]'), Number(stats.rejected || 0).toLocaleString());
        }
        function makeCell(row, value, className) { var cell = document.createElement('td'); if (className) cell.className = className; cell.textContent = value || '—'; row.appendChild(cell); return cell; }
        function renderRows(rows) {
            body.innerHTML = '';
            if (!rows.length) { setState(search.value.trim() ? 'No applications match your search and selected status.' : 'No applications found for this status.'); return; }
            rows.forEach(function (student) {
                var row = document.createElement('tr'); row.className = 'clickable-row'; row.tabIndex = 0; row.setAttribute('role', 'button'); row.setAttribute('aria-label', 'Inspect ' + student.name);
                makeCell(row, student.studentId, 'asv-td--id');
                var person = document.createElement('td'), wrap = document.createElement('div'), avatar = document.createElement('span'), details = document.createElement('div'), name = document.createElement('div'), course = document.createElement('div');
                wrap.className = 'asv-student-cell'; avatar.className = 'asv-avatar asv-avatar--indigo'; avatar.setAttribute('aria-hidden', 'true'); avatar.textContent = student.initials || 'ST';
                details.className = 'asv-student-cell__details'; name.className = 'asv-student-cell__name'; name.textContent = student.name || 'Student';
                course.className = 'asv-student-cell__course'; course.textContent = [student.program, student.yearLevel, student.section].filter(Boolean).join(' · ');
                details.appendChild(name); details.appendChild(course); wrap.appendChild(avatar); wrap.appendChild(details); person.appendChild(wrap); row.appendChild(person);
                makeCell(row, student.campus, 'asv-td--muted'); makeCell(row, student.email, 'asv-td--muted'); makeCell(row, student.date, 'asv-td--muted');
                var statusCell = document.createElement('td'), badge = document.createElement('span'), conf = statusConfig[student.status] || [student.status, 'asv-pill--pending'];
                badge.className = 'asv-pill ' + conf[1]; badge.textContent = conf[0]; statusCell.appendChild(badge); row.appendChild(statusCell);
                var actionCell = document.createElement('td'); actionCell.className = 'asv-td--right'; var inspect = document.createElement('button'); inspect.type = 'button'; inspect.className = 'asv-btn-open-dossier'; inspect.textContent = 'Inspect';
                inspect.addEventListener('click', function (e) { e.stopPropagation(); openDossier(student); }); actionCell.appendChild(inspect); row.appendChild(actionCell);
                row.addEventListener('click', function () { openDossier(student); });
                row.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openDossier(student); } });
                body.appendChild(row);
            });
        }
        function renderPagination(pageNumber) {
            pagination.innerHTML = '';
            var first = totalRows === 0 ? 0 : ((pageNumber - 1) * pageSize + 1), last = Math.min(pageNumber * pageSize, totalRows);
            paginationInfo.textContent = 'Showing ' + first + '–' + last + ' of ' + totalRows + ' submissions';
            countLabel.textContent = 'Showing ' + totalRows + ' Records';
            function button(label, target, disabled, active) {
                var b = document.createElement('button'); b.type = 'button'; b.className = 'asv-pagination__btn' + (active ? ' is-active' : '') + (disabled ? ' asv-pagination__btn--ghost' : ''); b.textContent = label; b.disabled = disabled;
                if (!disabled && target !== pageNumber) b.addEventListener('click', function () { load(target); });
                pagination.appendChild(b);
            }
            button('Previous', pageNumber - 1, pageNumber <= 1, false);
            var start = Math.max(1, Math.min(pageNumber - 2, totalPages - 4)), end = Math.min(totalPages, start + 4);
            for (var i = start; i <= end; i++) button(String(i), i, false, i === pageNumber);
            button('Next', pageNumber + 1, pageNumber >= totalPages, false);
        }
        function load(requestedPage, preserveFeedback) {
            currentPage = requestedPage || 1; var requestId = ++requestSequence; if (!preserveFeedback) showFeedback('', false); setState('Loading student applications…');
            var query = '?status=' + encodeURIComponent(status.value) + '&search=' + encodeURIComponent(search.value.trim()) + '&page=' + currentPage + '&pageSize=' + pageSize;
            fetch(endpoint + query, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(function (response) {
                if (!response.ok) throw new Error('Student applications could not be loaded. Try again.'); return response.json();
            }).then(function (result) {
                if (requestId !== requestSequence) return;
                if (!result || !result.success) throw new Error(result && result.message || 'Student applications could not be loaded.');
                currentPage = result.page; totalPages = result.totalPages; totalRows = result.total; updateStats(result.stats); renderRows(result.data || []); renderPagination(currentPage);
            }).catch(function (error) { if (requestId !== requestSequence) return; setState('Applications are unavailable.'); showFeedback(error.message, true); pagination.innerHTML = ''; paginationInfo.textContent = 'Unable to load records'; countLabel.textContent = 'Records unavailable'; ['[data-kpi-total]', '[data-kpi-pending]', '[data-kpi-approved]', '[data-kpi-rejected]'].forEach(function (selector) { text(page.querySelector(selector), '—'); }); });
        }
        function docElement(doc, label) {
            var section = document.createElement('section'); section.className = 'asv-real-document'; var heading = document.createElement('h4'); heading.textContent = label; section.appendChild(heading);
            if (!doc || !doc.url) { var missing = document.createElement('p'); missing.className = 'asv-document-message'; missing.textContent = 'No ' + label.toLowerCase() + ' was submitted.'; section.appendChild(missing); return section; }
            var loading = document.createElement('p'); loading.className = 'asv-document-message'; loading.textContent = 'Loading document preview…'; section.appendChild(loading);
            var frame;
            if (doc.type === 'pdf') { frame = document.createElement('iframe'); frame.src = doc.url; frame.title = label + ' document preview'; frame.loading = 'lazy'; }
            else { frame = document.createElement('img'); frame.src = doc.url; frame.alt = label + ' submitted by ' + (currentStudent ? currentStudent.name : 'student'); }
            frame.className = 'asv-real-document__preview';
            frame.addEventListener('load', function () { if (loading.parentNode) loading.remove(); });
            frame.addEventListener('error', function () { section.innerHTML = ''; var failed = document.createElement('p'); failed.className = 'asv-document-message'; failed.textContent = label + ' could not be previewed. It may be missing or inaccessible.'; section.appendChild(failed); });
            section.appendChild(frame); var link = document.createElement('a'); link.href = doc.url; link.target = '_blank'; link.rel = 'noopener'; link.textContent = 'Open or download'; section.appendChild(link); return section;
        }
        function openDossier(student) {
            currentStudent = student;
            text(page.querySelector('[data-dossier-kyc]'), 'Application ' + student.id);
            text(page.querySelector('[data-dossier-name]'), student.name);
            text(page.querySelector('[data-dossier-course]'), [student.program, student.yearLevel, student.section].filter(Boolean).join(' · '));
            text(page.querySelector('[data-dossier-id-chip]'), 'Student ID: ' + (student.studentId || '—'));
            text(page.querySelector('[data-dossier-campus]'), student.campus);
            text(page.querySelector('[data-dossier-email]'), student.email); text(page.querySelector('[data-dossier-phone]'), student.phone);
            text(page.querySelector('[data-dossier-registered]'), student.date); text(page.querySelector('[data-dossier-submitted]'), student.submittedAt);
            text(page.querySelector('[data-dossier-account-status]'), student.accountStatus); text(page.querySelector('[data-dossier-review-notes]'), student.reviewNotes);
            text(page.querySelector('[data-dossier-avatar]'), student.initials || 'ST');
            var pill = page.querySelector('[data-dossier-status-pill]'), cfg = statusConfig[student.status] || [student.status, 'asv-pill--pending']; pill.className = 'asv-pill ' + cfg[1]; pill.textContent = cfg[0];
            var docs = page.querySelector('.asv-doc-panel'); docs.innerHTML = ''; docs.appendChild(docElement(student.documents && student.documents.id, 'Student ID'));
            docs.appendChild(docElement(student.documents && student.documents.form, 'Registration document'));
            docs.appendChild(docElement(student.documents && student.documents.photo, '1×1 photo'));
            var pending = student.status === 'PENDING'; page.querySelector('[data-asv-action="approve"]').hidden = !pending; page.querySelector('[data-asv-action="reject"]').hidden = !pending;
            overlay.style.display = 'block'; overlay.setAttribute('aria-hidden', 'false'); modal.style.display = 'flex'; modal.setAttribute('aria-hidden', 'false'); document.body.style.overflow = 'hidden';
        }
        function closeDossier() { overlay.style.display = 'none'; overlay.setAttribute('aria-hidden', 'true'); modal.style.display = 'none'; modal.setAttribute('aria-hidden', 'true'); document.body.style.overflow = ''; currentStudent = null; }
        function postDecision(url, reason) {
            if (!currentStudent || currentStudent.status !== 'PENDING') return;
            var token = page.querySelector('input[name="__RequestVerificationToken"]');
            var form = new URLSearchParams(); form.set('__RequestVerificationToken', token ? token.value : ''); form.set('id', currentStudent.id); if (reason != null) form.set('reason', reason);
            var button = page.querySelector('[data-asv-action="' + (reason == null ? 'approve' : 'reject') + '"]'); if (button) button.disabled = true;
            fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', Accept: 'application/json' }, body: form.toString() }).then(function (response) {
                return response.json().catch(function () { return { success: false, message: response.status === 409 ? 'This application has already been reviewed.' : 'The review could not be saved.' }; }).then(function (data) { if (!response.ok || !data.success) throw new Error(data.message || 'The review could not be saved.'); return data; });
            }).then(function (data) { closeDossier(); showFeedback(data.message, false); load(currentPage, true); }).catch(function (error) { showFeedback(error.message, true); if (button) button.disabled = false; });
        }
        document.getElementById('btnResetFilters').addEventListener('click', function () { search.value = ''; status.value = 'PENDING'; load(1); });
        status.addEventListener('change', function () { load(1); });
        search.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(function () { load(1); }, 250); });
        document.getElementById('btnAsvCloseModal').addEventListener('click', closeDossier); overlay.addEventListener('click', closeDossier);
        document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && modal.style.display !== 'none') closeDossier(); });
        page.querySelector('[data-asv-action="approve"]').addEventListener('click', function () { if (currentStudent && window.confirm('Approve ' + currentStudent.name + ' for UniShare?')) postDecision(approveUrl, null); });
        page.querySelector('[data-asv-action="reject"]').addEventListener('click', function () {
            if (!currentStudent || !window.confirm('Reject this student verification application?')) return;
            var reason = window.prompt('Rejection reason (optional):', currentStudent.reviewNotes || ''); if (reason === null) return;
            postDecision(rejectUrl, reason);
        });
        load(1);
    });
}());
