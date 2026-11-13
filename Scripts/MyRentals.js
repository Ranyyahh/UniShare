/**
 * UniShare - MyRentals JavaScript
 * Handles:
 *  - Filter Modal open/close, focus management, backdrop click, Escape key
 *  - Status checkboxes multi-select and live count updates
 *    (nothing selected = All Statuses; Reset restores All Statuses)
 *  - Apply Filters
 *  - Reopening reflection of applied filters
 *  - Real-time search with clear button
 *  - Booking Date dropdown filtering (All Time, Today, Last 7 Days, Last 30 Days, This Month)
 *  - Multi-condition combined filtering (Status + Date + Search)
 *  - Client-side pagination (Prev, Next, Page X of Y, Showing range)
 *  - Empty states (No status selected vs No search/filter match)
 *  - Refresh button (reloads the data and resets the page to its initial state)
 *  - Rental Details modal (view only; close button, backdrop click, Escape)
 *  - Pending flow: Rental Request Details -> Accept Request / Confirm Decline?
 *  - Ongoing modal with live countdown and progress bar (timer stops when it closes)
 *  - Overdue modal (live overdue time + late fee), Returned modal, Report a Dispute
 *    (category, description, max 2 evidence images)
 *  - Accepted modal (two-party handover verification), Declined / Disputed / Completed modals
 *  - Shared confirmation step for Mark as Returned / Mark as Completed / Mark as Resolved
 *  Frontend / mock behaviour only: records change in the page, nothing is saved.
 */

(function () {
    'use strict';

    // Application state
    // The eight rental statuses, in the order they appear in the Filter modal. They are independent:
    // Overdue is not Ongoing, Returned is not Completed, Disputed is neither Returned nor Completed.
    // All eight applied = "All Statuses": every record is shown.
    var ALL_STATUSES = ['Pending', 'Accepted', 'Declined', 'Ongoing', 'Overdue', 'Returned', 'Disputed', 'Completed'];
    var DEFAULT_STATUSES = ALL_STATUSES.slice();
    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var PAGE_SIZE = 6;
    var REFERENCE_TODAY = new Date(2026, 10, 9); // Nov 09, 2026 (local project reference time)
    var MAX_DISPUTE_IMAGES = 2;
    var MAX_IMAGE_BYTES = 5 * 1024 * 1024; // 5 MB per image

    var state = {
        appliedStatuses: DEFAULT_STATUSES.slice(),
        appliedDate: 'All Time',
        appliedSearch: '',
        currentPage: 1,
        allRentals: []
    };

    // DOM Elements cache
    var elements = {
        tableBody: document.getElementById('myRentalsTableBody'),
        emptyState: document.getElementById('myRentalsEmptyState'),
        emptyStateTitle: document.getElementById('emptyStateTitle'),
        emptyStateText: document.getElementById('emptyStateText'),
        pagination: document.getElementById('myRentalsPagination'),
        paginationStart: document.getElementById('paginationStart'),
        paginationEnd: document.getElementById('paginationEnd'),
        paginationTotal: document.getElementById('paginationTotal'),
        paginationPageLabel: document.getElementById('paginationPageLabel'),
        btnPaginationPrev: document.getElementById('btnPaginationPrev'),
        btnPaginationNext: document.getElementById('btnPaginationNext'),
        searchInput: document.getElementById('myRentalsSearchInput'),
        searchClear: document.getElementById('myRentalsSearchClear'),
        dateFilter: document.getElementById('myRentalsDateFilter'),
        btnOpenFilterModal: document.getElementById('btnOpenFilterModal'),
        activeFilterBadge: document.getElementById('activeFilterBadge'),
        filterModal: document.getElementById('myRentalsFilterModal'),
        btnCloseFilterModal: document.getElementById('btnCloseFilterModal'),
        btnFilterReset: document.getElementById('btnFilterReset'),
        btnFilterApply: document.getElementById('btnFilterApply'),
        statusSelectedCount: document.getElementById('statusSelectedCount'),
        statusCheckboxes: document.querySelectorAll('.status-filter-checkbox'),
        selectAllStatuses: document.getElementById('chkSelectAllStatuses'),
        btnEmptyResetFilters: document.getElementById('btnEmptyResetFilters'),
        btnEmptyClearAll: document.getElementById('btnEmptyClearAll'),
        rawDataEl: document.getElementById('myRentalsRawData'),
        btnRefresh: document.getElementById('btnRefreshRentals'),
        detailsModal: document.getElementById('myRentalsDetailsModal'),
        btnCloseDetailsModal: document.getElementById('btnCloseDetailsModal'),
        btnDetailsClose: document.getElementById('btnDetailsClose'),

        // Pending / Accept / Decline / Ongoing modals
        pendingModal: document.getElementById('modalPendingDetails'),
        btnClosePendingDetails: document.getElementById('btnClosePendingDetails'),
        btnPendingDecline: document.getElementById('btnPendingDecline'),
        btnPendingAccept: document.getElementById('btnPendingAccept'),
        acceptModal: document.getElementById('modalAcceptConfirm'),
        btnAcceptCancel: document.getElementById('btnAcceptCancel'),
        btnAcceptConfirm: document.getElementById('btnAcceptConfirm'),
        declineModal: document.getElementById('modalDeclineConfirm'),
        btnCloseDeclineModal: document.getElementById('btnCloseDeclineModal'),
        btnDeclineCancel: document.getElementById('btnDeclineCancel'),
        btnDeclineConfirm: document.getElementById('btnDeclineConfirm'),
        declineReasonError: document.getElementById('declineReasonError'),
        declineExplanation: document.getElementById('declineExplanationText'),
        ongoingModal: document.getElementById('modalOngoing'),
        btnCloseOngoingModal: document.getElementById('btnCloseOngoingModal'),
        btnOngoingClose: document.getElementById('btnOngoingClose'),
        btnOngoingConfirmReturn: document.getElementById('btnOngoingConfirmReturn'),

        // Overdue / Return / Dispute modals
        overdueModal: document.getElementById('modalOverdue'),
        btnCloseOverdueModal: document.getElementById('btnCloseOverdueModal'),
        btnOverdueClose: document.getElementById('btnOverdueClose'),
        btnOverdueProceedReturn: document.getElementById('btnOverdueProceedReturn'),
        returnModal: document.getElementById('modalReturn'),
        btnCloseReturnModal: document.getElementById('btnCloseReturnModal'),
        btnReturnClose: document.getElementById('btnReturnClose'),
        btnReturnReportDispute: document.getElementById('btnReturnReportDispute'),
        btnReturnConfirm: document.getElementById('btnReturnConfirm'),
        disputeModal: document.getElementById('modalDispute'),
        btnCloseDisputeModal: document.getElementById('btnCloseDisputeModal'),
        btnDisputeCancel: document.getElementById('btnDisputeCancel'),
        btnDisputeSubmit: document.getElementById('btnDisputeSubmit'),
        disputeCategories: document.getElementById('disputeCategories'),
        disputeCategoryError: document.getElementById('disputeCategoryError'),
        disputeDescription: document.getElementById('disputeDescriptionText'),
        disputeDescError: document.getElementById('disputeDescError'),
        disputeImageInput: document.getElementById('disputeImageInput'),
        disputeImagePreviews: document.getElementById('disputeImagePreviews'),
        disputeImageCount: document.getElementById('disputeImageCount'),
        disputeImageError: document.getElementById('disputeImageError'),
        btnDisputeUpload: document.getElementById('btnDisputeUpload'),
        disputeUploadTitle: document.getElementById('disputeUploadTitle'),
        disputeUploadSub: document.getElementById('disputeUploadSub'),

        // Accepted / Declined / Disputed / Completed modals and the shared confirmation step
        acceptedModal: document.getElementById('modalAccepted'),
        btnCloseAcceptedModal: document.getElementById('btnCloseAcceptedModal'),
        btnAcceptedClose: document.getElementById('btnAcceptedClose'),
        btnAcceptedConfirmHandover: document.getElementById('btnAcceptedConfirmHandover'),
        declinedModal: document.getElementById('modalDeclined'),
        btnCloseDeclinedModal: document.getElementById('btnCloseDeclinedModal'),
        btnDeclinedClose: document.getElementById('btnDeclinedClose'),
        disputedModal: document.getElementById('modalDisputed'),
        btnCloseDisputedModal: document.getElementById('btnCloseDisputedModal'),
        btnDisputedClose: document.getElementById('btnDisputedClose'),
        btnDisputedResolve: document.getElementById('btnDisputedResolve'),
        completedModal: document.getElementById('modalCompleted'),
        btnCloseCompletedModal: document.getElementById('btnCloseCompletedModal'),
        btnCompletedClose: document.getElementById('btnCompletedClose'),
        statusConfirmModal: document.getElementById('modalStatusConfirm'),
        btnStatusConfirmCancel: document.getElementById('btnStatusConfirmCancel'),
        btnStatusConfirmOk: document.getElementById('btnStatusConfirmOk')
    };

    var detailsTrigger = null; // the View Details button that opened the details modal
    var isRefreshing = false;
    var detailsTimer = null;
    var activeDetailRental = null;
    var historyModal = document.getElementById('myRentalsHistoryModal');

    // The rental currently open in the status modals (Pending / Accept / Decline / Ongoing /
    // Overdue / Return / Dispute).
    //  returnTo:    modal to go back to when a confirmation (Accept / Decline) is cancelled
    //  returnFocus: control that opened the confirmation, so focus can go back to it
    var flow = { rental: null, trigger: null, returnTo: null, returnFocus: null };
    var countdownTimer = null;
    var overdueTimer = null;
    var thumbPlaceholders = {};
    var disputeImages = []; // { file, url } for the evidence images chosen in the Dispute modal

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------
    function init() {
        loadData();
        bindEvents();
        syncModalCheckboxes(state.appliedStatuses);
        updateModalCount();
        updateFilterBadge();
        updateSummaryCards();
        applyAllFiltersAndPaginate();
    }

    function loadData() {
        if (elements.rawDataEl && elements.rawDataEl.textContent) {
            try {
                state.allRentals = JSON.parse(elements.rawDataEl.textContent.trim());
            } catch (err) {
                console.error('UniShare MyRentals: could not parse raw data', err);
                state.allRentals = parseFromDom();
            }
        } else {
            state.allRentals = parseFromDom();
        }
    }

    function initialsOf(name) {
        var parts = String(name || '').replace(/^\s+|\s+$/g, '').split(/\s+/);
        if (!parts[0]) return '';
        return (parts[0].charAt(0) + (parts.length > 1 ? parts[parts.length - 1].charAt(0) : '')).toUpperCase();
    }

    function parseFromDom() {
        var rows = document.querySelectorAll('.my-rentals-row');
        var list = [];
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            list.push({
                RequestId: row.getAttribute('data-request-id') || '',
                ItemName: row.getAttribute('data-item') || '',
                BorrowerName: row.getAttribute('data-borrower') || '',
                BorrowerInitials: initialsOf(row.getAttribute('data-borrower') || ''),
                BorrowerProgram: (row.querySelector('.borrower-program') || {}).textContent || '',
                RequestDate: row.getAttribute('data-request-date') || '',
                RequestDateFormatted: (row.querySelector('.request-date-text') || {}).textContent || '',
                HandoverScheduleFormatted: (row.querySelector('.schedule-text') || {}).textContent || '',
                HandoverDate: row.getAttribute('data-date') || '',
                Duration: row.getAttribute('data-duration') || '',
                RentalFee: parseFloat(row.getAttribute('data-fee') || '0'),
                RentalFeeFormatted: (row.querySelector('.fee-text') || {}).textContent || '',
                Status: row.getAttribute('data-status') || '',
                Category: (row.querySelector('.item-category') || {}).textContent || ''
            });
        }
        return list;
    }

    // -------------------------------------------------------------------------
    // Filter Modal Logic
    // -------------------------------------------------------------------------
    function openFilterModal() {
        // Sync draft checkboxes with current applied statuses
        syncModalCheckboxes(state.appliedStatuses);
        updateModalCount();

        elements.filterModal.classList.add('is-open');
        elements.btnOpenFilterModal.setAttribute('aria-expanded', 'true');

        // Focus first checkbox
        if (elements.statusCheckboxes.length > 0) {
            elements.statusCheckboxes[0].focus();
        }
    }

    function isModalOpen(modalEl) {
        return !!modalEl && modalEl.classList.contains('is-open');
    }

    function hideFilterModal() {
        elements.filterModal.classList.remove('is-open');
        elements.btnOpenFilterModal.setAttribute('aria-expanded', 'false');
    }

    function closeFilterModal() {
        // Close without applying draft changes
        hideFilterModal();
        elements.btnOpenFilterModal.focus();
    }

    function syncModalCheckboxes(statusesToSelect) {
        for (var i = 0; i < elements.statusCheckboxes.length; i++) {
            var cb = elements.statusCheckboxes[i];
            cb.checked = statusesToSelect.indexOf(cb.value) !== -1;
        }
    }

    function getModalCheckedStatuses() {
        var checked = [];
        for (var i = 0; i < elements.statusCheckboxes.length; i++) {
            if (elements.statusCheckboxes[i].checked) {
                checked.push(elements.statusCheckboxes[i].value);
            }
        }
        return checked;
    }

    // Select All mirrors the individual boxes: ticked when all eight are, "mixed" when only some are
    function syncSelectAll() {
        var count = getModalCheckedStatuses().length;
        if (elements.selectAllStatuses) {
            elements.selectAllStatuses.checked = count === elements.statusCheckboxes.length;
            elements.selectAllStatuses.indeterminate = count > 0 && count < elements.statusCheckboxes.length;
        }
    }

    function updateModalCount() {
        syncSelectAll();
        var count = getModalCheckedStatuses().length;
        if (elements.statusSelectedCount) {
            var label = count + ' selected';
            if (count === ALL_STATUSES.length) {
                label = 'All statuses';
            } else if (count === 0) {
                label = 'None selected (shows all)';
            }
            elements.statusSelectedCount.textContent = label;
        }
    }

    function resetModalCheckboxes() {
        // Reset unticks every status. With nothing selected there is no status restriction,
        // so all rental records stay visible.
        syncModalCheckboxes([]);
        updateModalCount();
        state.appliedStatuses = [];
        updateFilterBadge();
        state.currentPage = 1;
        applyAllFiltersAndPaginate();
    }

    // Ticking boxes only changes the draft; nothing is filtered until Apply Filters is clicked.
    // Applying with nothing ticked means no status restriction (all records stay visible).
    function applyModalFilters() {
        state.appliedStatuses = getModalCheckedStatuses();
        updateFilterBadge();
        closeFilterModal();
        state.currentPage = 1;
        applyAllFiltersAndPaginate();
    }

    // The badge shows how many statuses are applied once the filter is narrowed.
    // It is hidden in the default All Statuses state.
    function updateFilterBadge() {
        if (elements.activeFilterBadge) {
            var n = state.appliedStatuses.length;
            elements.activeFilterBadge.textContent = n;
            elements.activeFilterBadge.style.display = (n > 0 && n < ALL_STATUSES.length) ? '' : 'none';
        }
    }

    // -------------------------------------------------------------------------
    // Multi-Filter Matching Logic
    // -------------------------------------------------------------------------
    function matchesStatus(item) {
        // Each status is matched on its own (no status stands in for another).
        if (state.appliedStatuses.length === 0) {
            return true;
        }
        return state.appliedStatuses.indexOf(item.Status) !== -1;
    }

    function matchesDate(item) {
        if (state.appliedDate === 'All Time') {
            return true;
        }

        var itemDate = parseItemDate(item.HandoverDate);
        if (!itemDate) {
            return true;
        }

        // Compare calendar days (not timestamps) so a record dated later today still counts as "0 days ago"
        var ref = REFERENCE_TODAY;
        var diffDays = Math.round((startOfDay(ref).getTime() - startOfDay(itemDate).getTime()) / (1000 * 60 * 60 * 24));

        if (state.appliedDate === 'Today') {
            return isSameDay(itemDate, ref);
        }

        if (state.appliedDate === 'Last 7 Days') {
            return diffDays >= 0 && diffDays <= 7;
        }

        if (state.appliedDate === 'Last 30 Days') {
            return diffDays >= 0 && diffDays <= 30;
        }

        if (state.appliedDate === 'This Month') {
            return itemDate.getFullYear() === ref.getFullYear() &&
                itemDate.getMonth() === ref.getMonth();
        }

        return true;
    }

    // Parses "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss" (treated as local time) and values with a
    // "Z" / "+08:00" offset. Done by hand because ES5 engines disagree on date-only / offset-less strings.
    function parseIsoDate(val) {
        if (!val) return null;
        if (val instanceof Date) return val;

        var dotNetDate = /^\/Date\((-?\d+)(?:[+-]\d{4})?\)\/$/.exec(String(val));
        if (dotNetDate) return new Date(parseInt(dotNetDate[1], 10));

        var m = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?$/.exec(String(val));
        if (!m) {
            var fallback = new Date(val);
            return isNaN(fallback.getTime()) ? null : fallback;
        }

        var y = parseInt(m[1], 10), mo = parseInt(m[2], 10) - 1, d = parseInt(m[3], 10);
        var h = parseInt(m[4] || '0', 10), mi = parseInt(m[5] || '0', 10), sec = parseInt(m[6] || '0', 10);

        if (m[7]) {
            var utc = Date.UTC(y, mo, d, h, mi, sec);
            if (m[7] !== 'Z') {
                var sign = m[7].charAt(0) === '-' ? -1 : 1;
                var digits = m[7].replace(/[^0-9]/g, '');
                var offsetMinutes = sign * (parseInt(digits.substr(0, 2), 10) * 60 + parseInt(digits.substr(2, 2), 10));
                utc -= offsetMinutes * 60000;
            }
            return new Date(utc);
        }

        return new Date(y, mo, d, h, mi, sec);
    }

    function parseItemDate(dateVal) {
        return parseIsoDate(dateVal);
    }

    function startOfDay(d) {
        return new Date(d.getFullYear(), d.getMonth(), d.getDate());
    }

    function isSameDay(d1, d2) {
        return d1.getFullYear() === d2.getFullYear() &&
            d1.getMonth() === d2.getMonth() &&
            d1.getDate() === d2.getDate();
    }

    function matchesSearch(item) {
        if (!state.appliedSearch) {
            return true;
        }

        var q = state.appliedSearch.toLowerCase();
        var haystacks = [
            item.RequestId || '',
            item.ItemName || '',
            item.BorrowerName || '',
            item.BorrowerProgram || '',
            item.RequestDateFormatted || '',
            item.Duration || '',
            item.RentalFeeFormatted || '',
            String(item.RentalFee || ''),
            item.Status || '',
            item.Category || ''
        ];

        for (var i = 0; i < haystacks.length; i++) {
            if (haystacks[i].toLowerCase().indexOf(q) !== -1) {
                return true;
            }
        }

        return false;
    }

    function getFilteredRentals() {
        return state.allRentals.filter(function (item) {
            return matchesStatus(item) && matchesDate(item) && matchesSearch(item);
        });
    }

    // -------------------------------------------------------------------------
    // Render and Pagination
    // -------------------------------------------------------------------------
    function applyAllFiltersAndPaginate() {
        var filtered = getFilteredRentals();
        var totalFiltered = filtered.length;

        if (totalFiltered === 0) {
            renderEmptyState();
            return;
        }

        // Hide empty state and show table body
        elements.emptyState.style.display = 'none';
        elements.tableBody.style.display = '';
        elements.pagination.style.display = 'flex';

        // Calculate pages
        var totalPages = Math.ceil(totalFiltered / PAGE_SIZE) || 1;
        if (state.currentPage > totalPages) {
            state.currentPage = totalPages;
        }
        if (state.currentPage < 1) {
            state.currentPage = 1;
        }

        var startIdx = (state.currentPage - 1) * PAGE_SIZE;
        var endIdx = Math.min(startIdx + PAGE_SIZE, totalFiltered);
        var pageItems = filtered.slice(startIdx, endIdx);

        renderRows(pageItems);

        // Update pagination UI
        elements.paginationStart.textContent = startIdx + 1;
        elements.paginationEnd.textContent = endIdx;
        elements.paginationTotal.textContent = totalFiltered;
        elements.paginationPageLabel.textContent = 'Page ' + state.currentPage + ' of ' + totalPages;

        elements.btnPaginationPrev.disabled = state.currentPage <= 1;
        elements.btnPaginationNext.disabled = state.currentPage >= totalPages;
    }

    function renderEmptyState() {
        elements.tableBody.style.display = 'none';
        elements.pagination.style.display = 'none';
        elements.emptyState.style.display = 'block';

        elements.emptyStateTitle.textContent = 'No rentals found';
        elements.emptyStateText.textContent = 'No rental records matched your active filters. Try adjusting your search query, status selection, or booking date.';
    }

    function renderRows(items) {
        var html = '';
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var statusClass = 'status-' + (item.Status || '').toLowerCase();

            html += '<tr class="my-rentals-row" data-request-id="' + esc(item.RequestId) + '">' +
                '<td class="td-request-id"><span class="request-id-badge">' + esc(item.RequestId) + '</span></td>' +
                '<td class="td-item">' +
                '<div class="item-cell">' +
                '<span class="item-name">' + esc(item.ItemName) + '</span>' +
                '<span class="item-category">' + esc(item.Category) + '</span>' +
                '</div>' +
                '</td>' +
                '<td class="td-borrower">' +
                '<div class="borrower-cell">' +
                '<div class="borrower-meta">' +
                '<span class="borrower-name">' + esc(item.BorrowerName) + '</span>' +
                '<span class="borrower-program">' + esc(item.BorrowerProgram) + '</span>' +
                '</div>' +
                '</div>' +
                '</td>' +
                '<td class="td-request-date"><span class="request-date-text">' + esc(item.RequestDateFormatted) + '</span></td>' +
                '<td class="td-schedule"><span class="schedule-text">' + esc(item.HandoverScheduleFormatted) + '</span></td>' +
                '<td class="td-duration"><span class="duration-text">' + esc(item.Duration) + '</span></td>' +
                '<td class="td-fee"><span class="fee-text">' + esc(item.RentalFeeFormatted) + '</span></td>' +
                '<td class="td-status">' +
                '<span class="status-badge ' + esc(statusClass) + '">' +
                '<span class="status-dot" aria-hidden="true"></span>' +
                '<span class="status-label">' + esc(item.Status) + '</span>' +
                '</span>' +
                '</td>' +
                '<td class="td-action text-right">' +
                '<button type="button" class="my-rentals-btn-view" data-request-id="' + esc(item.RequestId) + '" aria-label="View details for ' + esc(item.RequestId) + '">' +
                'View Details' +
                '</button>' +
                '</td>' +
                '</tr>';
        }
        elements.tableBody.innerHTML = html;
    }

    function esc(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // -------------------------------------------------------------------------
    // Event Listeners
    // -------------------------------------------------------------------------
    function bindEvents() {
        var historyButton = document.getElementById('btnRentalHistory');
        var historyClose = document.getElementById('btnCloseRentalHistory');
        var historyDone = document.getElementById('btnRentalHistoryDone');
        if (historyButton) historyButton.addEventListener('click', openRentalHistory);
        if (historyClose) historyClose.addEventListener('click', closeRentalHistory);
        if (historyDone) historyDone.addEventListener('click', closeRentalHistory);
        if (historyModal) historyModal.addEventListener('click', function (e) { if (e.target === historyModal) closeRentalHistory(); });
        // Modal Trigger
        elements.btnOpenFilterModal.addEventListener('click', function (e) {
            e.preventDefault();
            openFilterModal();
        });

        // Modal Close Button
        elements.btnCloseFilterModal.addEventListener('click', function (e) {
            e.preventDefault();
            closeFilterModal();
        });

        // Click outside modal dialog to dismiss without applying
        elements.filterModal.addEventListener('click', function (e) {
            if (e.target === elements.filterModal) {
                closeFilterModal();
            }
        });

        // Escape key dismisses whichever modal is open
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' || e.key === 'Esc' || e.keyCode === 27) {
                var openFlowModal = getOpenFlowModal();
                if (openFlowModal) {
                    dismissFlowModal(openFlowModal);
                } else if (isModalOpen(elements.detailsModal)) {
                    closeDetailsModal();
                } else if (historyModal && isModalOpen(historyModal)) {
                    closeRentalHistory();
                } else if (isModalOpen(elements.filterModal)) {
                    closeFilterModal();
                }
            }
        });

        // Live checkbox count updates
        for (var i = 0; i < elements.statusCheckboxes.length; i++) {
            elements.statusCheckboxes[i].addEventListener('change', function () {
                updateModalCount();
            });
        }

        // Select All ticks / unticks all eight statuses (draft only, applied with Apply Filters)
        if (elements.selectAllStatuses) {
            elements.selectAllStatuses.addEventListener('change', function () {
                syncModalCheckboxes(this.checked ? ALL_STATUSES : []);
                updateModalCount();
            });
        }

        // Modal Reset
        elements.btnFilterReset.addEventListener('click', function (e) {
            e.preventDefault();
            resetModalCheckboxes();
        });

        // Modal Apply
        elements.btnFilterApply.addEventListener('click', function (e) {
            e.preventDefault();
            applyModalFilters();
        });

        // Search Input
        elements.searchInput.addEventListener('input', function () {
            state.appliedSearch = this.value.trim();
            elements.searchClear.style.display = state.appliedSearch ? 'grid' : 'none';
            state.currentPage = 1;
            applyAllFiltersAndPaginate();
        });

        // Search Clear Button
        elements.searchClear.addEventListener('click', function () {
            elements.searchInput.value = '';
            state.appliedSearch = '';
            this.style.display = 'none';
            elements.searchInput.focus();
            state.currentPage = 1;
            applyAllFiltersAndPaginate();
        });

        // Booking Date Dropdown
        elements.dateFilter.addEventListener('change', function () {
            state.appliedDate = this.value;
            state.currentPage = 1;
            applyAllFiltersAndPaginate();
        });

        // Pagination Controls
        elements.btnPaginationPrev.addEventListener('click', function () {
            if (state.currentPage > 1) {
                state.currentPage--;
                applyAllFiltersAndPaginate();
                scrollToTable();
            }
        });

        elements.btnPaginationNext.addEventListener('click', function () {
            var filtered = getFilteredRentals();
            var totalPages = Math.ceil(filtered.length / PAGE_SIZE);
            if (state.currentPage < totalPages) {
                state.currentPage++;
                applyAllFiltersAndPaginate();
                scrollToTable();
            }
        });

        // Empty state quick actions
        if (elements.btnEmptyResetFilters) {
            elements.btnEmptyResetFilters.addEventListener('click', function () {
                state.appliedStatuses = DEFAULT_STATUSES.slice();
                updateFilterBadge();
                state.currentPage = 1;
                applyAllFiltersAndPaginate();
            });
        }

        if (elements.btnEmptyClearAll) {
            elements.btnEmptyClearAll.addEventListener('click', function () {
                state.appliedStatuses = DEFAULT_STATUSES.slice();
                state.appliedDate = 'All Time';
                state.appliedSearch = '';
                elements.dateFilter.value = 'All Time';
                elements.searchInput.value = '';
                elements.searchClear.style.display = 'none';
                updateFilterBadge();
                state.currentPage = 1;
                applyAllFiltersAndPaginate();
            });
        }

        // Table Action buttons delegation (rows are re-rendered, so listen on the tbody)
        elements.tableBody.addEventListener('click', function (e) {
            var btn = findAncestorWithClass(e.target, 'my-rentals-btn-view', elements.tableBody);
            if (btn) {
                e.preventDefault();
                openRentalModal(btn.getAttribute('data-request-id'), btn);
            }
        });

        bindFlowModalEvents();

        // Details modal: close button, footer Close button, click on backdrop
        elements.btnCloseDetailsModal.addEventListener('click', function (e) {
            e.preventDefault();
            closeDetailsModal();
        });

        elements.btnDetailsClose.addEventListener('click', function (e) {
            e.preventDefault();
            closeDetailsModal();
        });

        elements.detailsModal.addEventListener('click', function (e) {
            if (e.target === elements.detailsModal) {
                closeDetailsModal();
            }
        });

        // Refresh
        elements.btnRefresh.addEventListener('click', function (e) {
            e.preventDefault();
            refreshRentals();
        });
    }

    // -------------------------------------------------------------------------
    // Rental Details Modal (view only)
    // -------------------------------------------------------------------------
    function findAncestorWithClass(node, className, stopAt) {
        while (node && node !== stopAt) {
            if (node.nodeType === 1 && (' ' + node.className + ' ').indexOf(' ' + className + ' ') !== -1) {
                return node;
            }
            node = node.parentNode;
        }
        return null;
    }

    function findRentalById(requestId) {
        for (var i = 0; i < state.allRentals.length; i++) {
            if (state.allRentals[i].RequestId === requestId) {
                return state.allRentals[i];
            }
        }
        return null;
    }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) {
            el.textContent = (value === null || value === undefined || value === '') ? '\u2014' : value;
        }
    }

    function openDetailsModal(requestId, triggerEl) {
        var rental = findRentalById(requestId);
        if (!rental) {
            return;
        }

        setText('detailsHeaderRequestId', rental.RequestId);
        setText('detailsRequestId', rental.RequestId);
        setText('detailsItemName', rental.ItemName);
        setText('detailsCategory', rental.Category);
        setText('detailsBorrowerInitials', rental.BorrowerInitials);
        setText('detailsBorrowerName', rental.BorrowerName);
        setText('detailsBorrowerProgram', rental.BorrowerProgram);
        setText('detailsRequestDate', rental.RequestDateFormatted);
        setText('detailsHandover', rental.HandoverScheduleFormatted);
        setText('detailsDuration', rental.Duration);
        setText('detailsFee', rental.RentalFeeFormatted);
        setText('detailsRequestNote', rental.RequestNote || 'No note was included with the request.');
        setText('detailsStatusLabel', rental.Status);
        var ownerConfirmed = !!rental.OwnerHandoverConfirmed;
        var borrowerConfirmed = !!rental.BorrowerHandoverConfirmed;
        var ownerState = document.getElementById('detailsOwnerConfirmation');
        var borrowerState = document.getElementById('detailsBorrowerConfirmation');
        setText('detailsOwnerConfirmation', 'Owner: ' + (ownerConfirmed ? 'Confirmed' : 'Awaiting Confirmation'));
        setText('detailsBorrowerConfirmation', 'Borrower: ' + (borrowerConfirmed ? 'Confirmed' : 'Awaiting Confirmation'));
        ownerState.className = ownerConfirmed ? 'is-confirmed' : 'is-awaiting';
        borrowerState.className = borrowerConfirmed ? 'is-confirmed' : 'is-awaiting';
        activeDetailRental = rental;
        var timerField = document.getElementById('detailsTimerField');
        if (timerField) timerField.hidden = !(ownerConfirmed && borrowerConfirmed && rental.HandoverDate);
        updateDetailsTimer();
        if (detailsTimer) clearInterval(detailsTimer);
        if (timerField && !timerField.hidden) detailsTimer = setInterval(updateDetailsTimer, 1000);

        var badge = document.getElementById('detailsStatusBadge');
        if (badge) {
            badge.className = 'status-badge status-' + String(rental.Status || '').toLowerCase();
        }

        detailsTrigger = triggerEl || null;
        elements.detailsModal.classList.add('is-open');
        elements.btnCloseDetailsModal.focus();
    }

    function hideDetailsModal() {
        elements.detailsModal.classList.remove('is-open');
        if (detailsTimer) clearInterval(detailsTimer);
        detailsTimer = null;
    }

    function updateDetailsTimer() {
        var el = document.getElementById('detailsTimer');
        if (!el || !activeDetailRental) return;
        var start = parseIsoDate(activeDetailRental.HandoverDate);
        var hours = Number(activeDetailRental.DurationHours) || 1;
        if (!start) { el.textContent = 'Timer starts after both confirmations.'; return; }
        var seconds = Math.max(0, Math.floor((start.getTime() + hours * 3600000 - Date.now()) / 1000));
        el.textContent = seconds > 0 ? 'Time remaining: ' + formatClock(seconds * 1000) : 'Rental period ended';
    }

    function openRentalHistory() {
        var content = document.getElementById('myRentalsHistoryContent');
        historyModal.classList.add('is-open');
        content.textContent = 'Loading history…';
        var root = document.querySelector('.my-rentals-page');
        fetch(root.getAttribute('data-history-url'), { credentials: 'same-origin' }).then(function (r) { if (!r.ok) throw new Error(); return r.json(); }).then(function (rows) {
            if (!rows.length) { content.innerHTML = '<p class="my-rentals-history-empty">No rental history yet.</p>'; return; }
            var table = '<div class="my-rentals-history-table-wrap"><table class="my-rentals-history-table"><thead><tr><th>Request ID</th><th>Item</th><th>Participant</th><th>Your role</th><th>Request Date</th><th>Status</th><th>Fee</th></tr></thead><tbody>';
            rows.forEach(function (r) { table += '<tr><td>' + esc(r.RequestId) + '</td><td>' + esc(r.ItemName) + '</td><td>' + esc(r.Participant) + '</td><td>' + esc(r.Role) + '</td><td>' + esc(r.RequestDateFormatted) + '</td><td>' + esc(r.Status) + '</td><td>' + esc(r.RentalFeeFormatted) + '</td></tr>'; });
            content.innerHTML = table + '</tbody></table></div>';
        }).catch(function () { content.textContent = 'History could not be loaded. Please try again.'; });
        document.getElementById('btnCloseRentalHistory').focus();
    }

    function closeRentalHistory() { if (historyModal) historyModal.classList.remove('is-open'); }

    function closeDetailsModal() {
        hideDetailsModal();
        if (detailsTrigger && document.body.contains(detailsTrigger)) {
            detailsTrigger.focus();
        }
        detailsTrigger = null;
    }

    // -------------------------------------------------------------------------
    // Pending / Accept / Decline / Ongoing modals
    // -------------------------------------------------------------------------
    // Chooses the modal for the clicked row. Every status has its own modal; the generic
    // (view only) Rental Details modal is only a fallback for an unexpected status value.
    function openRentalModal(requestId, triggerEl) {
        var rental = findRentalById(requestId);
        var status = rental ? rental.Status : '';
        if (status === 'Pending') {
            openPendingModal(rental, triggerEl);
        } else if (status === 'Accepted') {
            openAcceptedModal(rental, triggerEl);
        } else if (status === 'Declined') {
            openDeclinedModal(rental, triggerEl);
        } else if (status === 'Ongoing') {
            openOngoingModal(rental, triggerEl);
        } else if (status === 'Overdue') {
            openOverdueModal(rental, triggerEl);
        } else if (status === 'Returned') {
            openReturnModal(rental, triggerEl);
        } else if (status === 'Disputed') {
            openDisputedModal(rental, triggerEl);
        } else if (status === 'Completed') {
            openCompletedModal(rental, triggerEl);
        } else {
            openDetailsModal(requestId, triggerEl);
        }
    }

    function getFlowModals() {
        return [elements.pendingModal, elements.acceptModal, elements.declineModal, elements.ongoingModal,
        elements.overdueModal, elements.returnModal, elements.disputeModal,
        elements.acceptedModal, elements.declinedModal, elements.disputedModal, elements.completedModal,
        elements.statusConfirmModal];
    }

    function getOpenFlowModal() {
        var list = getFlowModals();
        for (var i = 0; i < list.length; i++) {
            if (isModalOpen(list[i])) {
                return list[i];
            }
        }
        return null;
    }

    function hideFlowModals() {
        var list = getFlowModals();
        for (var i = 0; i < list.length; i++) {
            if (list[i]) {
                list[i].classList.remove('is-open');
            }
        }
        stopCountdown();
    }

    function showFlowModal(modalEl, focusEl) {
        hideFlowModals();
        modalEl.classList.add('is-open');
        if (focusEl) {
            focusEl.focus();
        }
        // Live timers only run while their modal is on screen (also when coming back to it)
        if (flow.rental) {
            if (modalEl === elements.ongoingModal) {
                startCountdown(flow.rental);
            } else if (modalEl === elements.overdueModal) {
                startOverdueTimer(flow.rental);
            }
        }
    }

    function endFlow(restoreFocus) {
        var requestId = flow.rental ? flow.rental.RequestId : null;
        hideFlowModals();
        flow.rental = null;
        flow.trigger = null;
        flow.returnTo = null;
        flow.returnFocus = null;
        if (restoreFocus && requestId) {
            focusViewButton(requestId);
        }
    }

    // Rows are re-rendered, so look the button up again instead of keeping the old element
    function focusViewButton(requestId) {
        var buttons = elements.tableBody.querySelectorAll('.my-rentals-btn-view');
        for (var i = 0; i < buttons.length; i++) {
            if (buttons[i].getAttribute('data-request-id') === requestId) {
                buttons[i].focus();
                return;
            }
        }
    }

    // Closing a confirmation (Cancel / backdrop / Escape) goes back to the previous modal;
    // closing anything else ends the flow.
    function dismissFlowModal(modalEl) {
        if (modalEl === elements.acceptModal || modalEl === elements.declineModal || modalEl === elements.disputeModal ||
            modalEl === elements.statusConfirmModal) {
            cancelConfirmation();
        } else {
            endFlow(true);
        }
    }

    function cancelConfirmation() {
        if (flow.returnTo) {
            showFlowModal(flow.returnTo, flow.returnFocus);
        } else {
            endFlow(true);
        }
    }

    function setThumb(id, url, altText) {
        var el = document.getElementById(id);
        if (!el) {
            return;
        }
        if (!thumbPlaceholders.hasOwnProperty(id)) {
            thumbPlaceholders[id] = el.innerHTML;
        }
        el.innerHTML = thumbPlaceholders[id];
        el.setAttribute('aria-hidden', 'true');
        if (url) {
            var img = document.createElement('img');
            img.alt = altText || '';
            img.src = url;
            el.innerHTML = '';
            el.appendChild(img);
            el.setAttribute('aria-hidden', 'false');
        }
    }

    // The four overview cards are counted from the records, so they always match the table.
    // Needs Attention = Overdue + Disputed. The cards are display-only (no click actions).
    function updateSummaryCards() {
        var counts = { total: state.allRentals.length, pending: 0, active: 0, overdue: 0, disputed: 0 };
        for (var i = 0; i < state.allRentals.length; i++) {
            var st = state.allRentals[i].Status;
            if (st === 'Pending') {
                counts.pending++;
            } else if (st === 'Ongoing') {
                counts.active++;
            } else if (st === 'Overdue') {
                counts.overdue++;
            } else if (st === 'Disputed') {
                counts.disputed++;
            }
        }
        setSummaryCard('total', counts.total);
        setSummaryCard('pending', counts.pending);
        setSummaryCard('active', counts.active);
        setSummaryCard('attention', counts.overdue + counts.disputed);
        var breakdown = document.getElementById('summaryAttentionBreakdown');
        if (breakdown) {
            breakdown.textContent = 'Overdue: ' + counts.overdue + ' \u00B7 Disputed: ' + counts.disputed;
        }
    }

    function setSummaryCard(key, value) {
        var el = document.querySelector('[data-summary-value="' + key + '"]');
        if (el) {
            el.textContent = String(value);
        }
    }

    // "Nov 09, 2026" and "Nov 09, 2026 • 4:30 PM", the same formats the controller produces
    function formatDateOnly(d) {
        return MONTHS[d.getMonth()] + ' ' + pad2(d.getDate()) + ', ' + d.getFullYear();
    }

    function formatDateTime(d) {
        return formatDateOnly(d) + ' \u2022 ' + formatTimeOfDay(d);
    }

    // --- Pending ---
    function openPendingModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        setText('pendingRequestId', rental.RequestId);
        setText('pendingItemCategory', rental.Category);
        setText('pendingItemName', rental.ItemName);
        setText('pendingItemLocation', rental.ItemLocation);
        setThumb('pendingItemThumb', rental.ItemImageUrl, rental.ItemName);
        setText('pendingFeeValue', rental.RentalFeeFormatted);
        setText('pendingBorrowerInitials', rental.BorrowerInitials);
        setText('pendingBorrowerName', rental.BorrowerName);
        setText('pendingBorrowerProgram', rental.BorrowerProgram);
        setText('pendingHandoverDate', rental.HandoverDateFormatted);
        setText('pendingHandoverTime', rental.HandoverTimeFormatted);
        setText('pendingDuration', rental.Duration);
        setText('pendingHandoverLocation', rental.HandoverLocation);
        setText('pendingReturnSchedule', rental.ExpectedReturnFormatted);
        setText('pendingFeeBreakdown', rental.RentalFeeFormatted);
        setText('pendingNote', rental.RequestNote);

        showFlowModal(elements.pendingModal, elements.btnClosePendingDetails);
    }

    // --- Accept ---
    function openAcceptModal() {
        var rental = flow.rental;
        if (!rental || rental.Status !== 'Pending') {
            return;
        }

        setText('acceptItemName', rental.ItemName);
        setText('acceptBorrower', 'Borrower: ' + rental.BorrowerName);
        setText('acceptRequestId', rental.RequestId);
        setText('acceptHandoverDate', rental.HandoverDateFormatted);
        setText('acceptHandoverTime', rental.HandoverTimeFormatted);
        setText('acceptLocation', rental.HandoverLocation);
        setText('acceptExpectedReturn', rental.ExpectedReturnFormatted);
        setText('acceptFee', rental.RentalFeeFormatted);
        setThumb('acceptItemThumb', rental.ItemImageUrl, rental.ItemName);

        flow.returnTo = elements.pendingModal;
        flow.returnFocus = elements.btnPendingAccept;
        showFlowModal(elements.acceptModal, elements.btnAcceptCancel);
    }

    function confirmAccept() {
        var rental = flow.rental;
        if (!rental || rental.Status !== 'Pending') {
            return;
        }

        elements.btnAcceptConfirm.disabled = true;
        postRentalAction('data-decide-url', rental.BookingObjectId, function (result) {
            elements.btnAcceptConfirm.disabled = false;
            if (!result || !result.ok) { window.alert(result && result.message ? result.message : 'The request could not be accepted.'); return; }
            window.location.reload();
        }, { accept: 'true' });
    }

    // --- Decline ---
    function getDeclineRadios() {
        return elements.declineModal.querySelectorAll('input[name="declineReason"]');
    }

    function resetDeclineForm() {
        var radios = getDeclineRadios();
        for (var i = 0; i < radios.length; i++) {
            radios[i].checked = false;
        }
        elements.declineExplanation.value = '';
        elements.declineReasonError.style.display = 'none';
        syncDeclineLabels();
    }

    // Mirrors the checked radio onto its label so the selected option has a clear visual state
    function syncDeclineLabels() {
        var radios = getDeclineRadios();
        for (var i = 0; i < radios.length; i++) {
            var label = radios[i].parentNode;
            if (radios[i].checked) {
                label.classList.add('is-checked');
            } else {
                label.classList.remove('is-checked');
            }
        }
    }

    function openDeclineModal() {
        var rental = flow.rental;
        if (!rental || rental.Status !== 'Pending') {
            return;
        }

        setText('declineRequestId', rental.RequestId);
        setText('declineItemName', rental.ItemName);
        setText('declineBorrower', rental.BorrowerName);
        resetDeclineForm();

        flow.returnTo = elements.pendingModal;
        flow.returnFocus = elements.btnPendingDecline;
        showFlowModal(elements.declineModal, getDeclineRadios()[0]);
    }

    function confirmDecline() {
        var rental = flow.rental;
        if (!rental || rental.Status !== 'Pending') {
            return;
        }

        var radios = getDeclineRadios();
        var chosen = null;
        for (var i = 0; i < radios.length; i++) {
            if (radios[i].checked) {
                chosen = radios[i];
                break;
            }
        }

        if (!chosen) {
            elements.declineReasonError.style.display = 'block';
            radios[0].focus();
            return;
        }

        elements.btnDeclineConfirm.disabled = true;
        postRentalAction('data-decide-url', rental.BookingObjectId, function (result) {
            elements.btnDeclineConfirm.disabled = false;
            if (!result || !result.ok) { window.alert(result && result.message ? result.message : 'The request could not be declined.'); return; }
            window.location.reload();
        }, { accept: 'false', reason: chosen.value, notes: elements.declineExplanation.value.replace(/^\s+|\s+$/g, '') });
    }

    // --- Accepted: two-party handover verification ---
    function openAcceptedModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        fillStatusModal('accepted', rental);
        renderHandoverVerification(rental);

        showFlowModal(elements.acceptedModal, elements.btnCloseAcceptedModal);
    }

    // One side of the verification: card state, message and the "confirmed on" / "waiting" line
    function renderVerificationParty(who, name, confirmed, confirmedAt, awaitingFoot) {
        var card = document.getElementById('accepted' + who + 'Card');
        var chip = document.getElementById('accepted' + who + 'Chip');
        var foot = document.getElementById('accepted' + who + 'Foot');
        var role = who.toLowerCase();

        setText('accepted' + who + 'CardName', name);
        setText('accepted' + who + 'Chip', confirmed ? 'Confirmed' : 'Awaiting Confirmation');
        setText('accepted' + who + 'Msg', confirmed
            ? 'The ' + role + ' has confirmed the physical handover.'
            : 'The ' + role + ' hasn\u2019t confirmed the physical handover yet.');
        setText('accepted' + who + 'Foot', confirmed
            ? 'Confirmed on ' + (confirmedAt || 'an earlier date')
            : awaitingFoot);

        card.className = 'rm-verify-card ' + (confirmed ? 'is-confirmed' : 'is-awaiting');
        chip.className = 'rm-verify-chip ' + (confirmed ? 'is-confirmed' : 'is-awaiting');
        foot.className = 'rm-verify-foot ' + (confirmed ? 'is-confirmed' : 'is-awaiting');
    }

    function renderHandoverVerification(rental) {
        var ownerDone = !!rental.OwnerHandoverConfirmed;
        var borrowerDone = !!rental.BorrowerHandoverConfirmed;
        var count = (ownerDone ? 1 : 0) + (borrowerDone ? 1 : 0);

        var progress = document.getElementById('acceptedVerifyProgress');
        progress.textContent = count + ' of 2 Verified';
        progress.className = 'rm-verify-progress' + (count === 1 ? ' is-partial' : '');

        var notice = document.getElementById('acceptedHandoverNoticeText');
        if (notice) {
            notice.textContent = ownerDone && borrowerDone
                ? 'Both parties confirmed. The rental is active and its timer has started.'
                : ownerDone
                    ? 'Owner confirmed. Waiting for the borrower to confirm in their Borrowed Items page.'
                    : borrowerDone
                        ? 'Borrower confirmed. Waiting for the owner to confirm the handover.'
                        : 'The rental timer will only start after both parties confirm the physical handover in person.';
        }

        renderVerificationParty('Owner', rental.OwnerName, ownerDone, rental.OwnerHandoverConfirmedAt, 'Your confirmation required');
        renderVerificationParty('Borrower', rental.BorrowerName, borrowerDone, rental.BorrowerHandoverConfirmedAt, 'Waiting for the borrower');

        // The owner can confirm only once; after that the button stays disabled (no duplicate confirmations)
        var btn = elements.btnAcceptedConfirmHandover;
        btn.disabled = ownerDone;
        btn.textContent = ownerDone ? '\u2713 Handover Confirmed' : '\u2713 Confirm Handover';
        btn.title = ownerDone ? 'You have already confirmed. Waiting for the borrower to confirm.' : '';
    }

    // The rental stays Accepted until BOTH parties have confirmed. Only then does it become Ongoing
    // and the rental timer starts from that moment.
    function confirmHandover() {
        var rental = flow.rental;
        if (!rental || rental.Status !== 'Accepted' || rental.OwnerHandoverConfirmed) {
            return;
        }
        elements.btnAcceptedConfirmHandover.disabled = true;
        postHandoverCondition(rental.BookingObjectId, function (conditionResult) {
            if (!conditionResult || !conditionResult.ok) {
                elements.btnAcceptedConfirmHandover.disabled = false;
                window.alert(conditionResult && conditionResult.message ? conditionResult.message : 'Handover condition could not be saved.');
                return;
            }
            postRentalAction('data-handover-url', rental.BookingObjectId, function (result) {
                if (!result || !result.ok) {
                    elements.btnAcceptedConfirmHandover.disabled = false;
                    window.alert(result && result.message ? result.message : 'Handover confirmation could not be saved.');
                    return;
                }
                rental.OwnerHandoverConfirmed = !!result.ownerConfirmed;
                rental.BorrowerHandoverConfirmed = !!result.borrowerConfirmed;
                rental.Status = result.status === 'ONGOING' ? 'Ongoing' : 'Accepted';
                rental.HandoverDate = result.handoverAt || rental.HandoverDate;
                rental.OwnerHandoverConfirmedAt = formatDateTime(new Date());
                renderHandoverVerification(rental);
                if (result.ownerConfirmed && result.borrowerConfirmed) window.location.reload();
            });
        });
    }

    function postRentalAction(urlAttribute, bookingId, callback, extraFields) {
        var root = document.querySelector('.my-rentals-page');
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var body = new URLSearchParams();
        body.append('id', bookingId || '');
        Object.keys(extraFields || {}).forEach(function (key) { body.append(key, extraFields[key]); });
        if (token) body.append('__RequestVerificationToken', token.value);
        fetch(root.getAttribute(urlAttribute), { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'XMLHttpRequest' }, body: body.toString() })
            .then(function (r) { return r.json(); }).then(callback)
            .catch(function () { callback({ ok: false, message: 'The update could not be saved. Refresh the page and try again.' }); });
    }

    function postHandoverCondition(bookingId, callback) {
        var input = document.getElementById('ownerHandoverPhotos');
        var files = input ? input.files : [];
        if (files.length > 5) { callback({ ok: false, message: 'Upload no more than five condition photos.' }); return; }
        var form = new FormData();
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        form.append('id', bookingId || ''); form.append('phase', 'HANDOVER');
        form.append('condition', document.getElementById('ownerHandoverCondition').value);
        form.append('notes', document.getElementById('ownerHandoverNotes').value);
        if (token) form.append('__RequestVerificationToken', token.value);
        Array.prototype.forEach.call(files, function (file) { form.append('photos', file, file.name); });
        var root = document.querySelector('.my-rentals-page');
        fetch(root.getAttribute('data-condition-url'), { method: 'POST', credentials: 'same-origin', body: form })
            .then(function (r) { return r.json(); }).then(callback)
            .catch(function () { callback({ ok: false, message: 'Handover condition could not be saved.' }); });
    }

    function startRental(rental) {
        var now = new Date();
        now = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours(), now.getMinutes(), 0);
        var hours = Number(rental.DurationHours) || 1;
        var end = new Date(now.getTime() + hours * 3600000);

        rental.Status = 'Ongoing';
        rental.HandoverDate = now;
        rental.HandoverDateFormatted = formatDateOnly(now);
        rental.HandoverTimeFormatted = formatTimeOfDay(now);
        rental.HandoverScheduleFormatted = formatDateTime(now);
        rental.ExpectedReturnDate = end;
        rental.ExpectedReturnFormatted = formatDateTime(end);
    }

    // --- Declined (view only) ---
    function openDeclinedModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        fillStatusModal('declined', rental);
        setText('declinedReason', rental.DeclineReason || 'No reason was recorded');
        setText('declinedAt', rental.DeclinedAtFormatted);
        var hasNote = !!rental.DeclineNote;
        document.getElementById('declinedNoteRow').style.display = hasNote ? '' : 'none';
        setText('declinedNote', rental.DeclineNote);

        showFlowModal(elements.declinedModal, elements.btnCloseDeclinedModal);
    }

    // --- Disputed ---
    function openDisputedModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        fillStatusModal('disputed', rental);
        setText('disputedCategory', rental.DisputeCategory);
        setText('disputedDescription', rental.DisputeDescription);
        setText('disputedSubmittedAt', rental.DisputeSubmittedAtFormatted);
        renderDisputeEvidence(rental.DisputeEvidenceUrls || []);

        showFlowModal(elements.disputedModal, elements.btnCloseDisputedModal);
    }

    // Built with DOM methods; shows the submitted images, or a short note when there are none
    function renderDisputeEvidence(urls) {
        var box = document.getElementById('disputedEvidence');
        var empty = document.getElementById('disputedEvidenceEmpty');
        box.innerHTML = '';

        for (var i = 0; i < urls.length; i++) {
            var link = document.createElement('a');
            link.className = 'rm-evidence-thumb';
            link.href = urls[i];
            link.target = '_blank';
            link.rel = 'noopener';
            link.title = 'Open evidence image ' + (i + 1);

            var img = document.createElement('img');
            img.src = urls[i];
            img.alt = 'Submitted evidence image ' + (i + 1);
            link.appendChild(img);
            box.appendChild(link);
        }

        box.style.display = urls.length ? '' : 'none';
        empty.style.display = urls.length ? 'none' : '';
    }

    // --- Completed (view only) ---
    function openCompletedModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        fillStatusModal('completed', rental);
        setText('completedRequestedOn', rental.RequestDateFormatted);
        setText('completedStartedOn', rental.HandoverScheduleFormatted);
        setText('completedReturnedOn', rental.ReturnedAtFormatted);
        setText('completedCompletedOn', rental.CompletedAtFormatted);

        var lateFee = Number(rental.LateFee) || 0;
        document.getElementById('completedLateRow').style.display = lateFee > 0 ? '' : 'none';
        setText('completedLateFee', formatPeso(lateFee));
        document.getElementById('completedDisputeRow').style.display = rental.DisputeResolved ? '' : 'none';
        setText('completedDisputeNote', (rental.DisputeCategory || 'Reported issue') + ' \u2014 marked as resolved');
        setText('completedTotalFee', formatPeso((Number(rental.RentalFee) || 0) + lateFee));

        showFlowModal(elements.completedModal, elements.btnCloseCompletedModal);
    }

    // --- Ongoing ---
    function openOngoingModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        var ownerText = rental.OwnerName ? 'Owner: ' + rental.OwnerName + (rental.OwnerProgram ? ' \u2022 ' + rental.OwnerProgram : '') : '';

        setText('ongoingRequestId', rental.RequestId);
        setText('ongoingItemCategory', rental.Category);
        setText('ongoingItemName', rental.ItemName);
        setText('ongoingOwner', ownerText);
        setText('ongoingLocation', rental.ItemLocation);
        setThumb('ongoingItemThumb', rental.ItemImageUrl, rental.ItemName);
        setText('ongoingFeeValue', rental.HourlyRateFormatted);
        setText('ongoingBorrowerInitials', rental.BorrowerInitials);
        setText('ongoingBorrowerName', rental.BorrowerName);
        setText('ongoingBorrowerProgram', rental.BorrowerProgram);
        setText('ongoingHandoverDate', rental.HandoverDateFormatted);
        setText('ongoingHandoverTime', rental.HandoverTimeFormatted);
        setText('ongoingDuration', rental.Duration);
        setText('ongoingHandoverLocation', rental.HandoverLocation);
        setText('ongoingExpectedReturn', rental.ExpectedReturnFormatted);
        setText('ongoingFeeBreakdown', rental.RentalFeeFormatted);
        setText('ongoingStartedLabel', rental.HandoverScheduleFormatted);
        setText('ongoingEndLabel', rental.ExpectedReturnFormatted);

        showFlowModal(elements.ongoingModal, elements.btnCloseOngoingModal);
    }

    function pad2(n) {
        return n < 10 ? '0' + n : String(n);
    }

    function formatClock(ms) {
        var total = Math.max(0, Math.ceil(ms / 1000));
        var h = Math.floor(total / 3600);
        var m = Math.floor((total % 3600) / 60);
        var sec = total % 60;
        return pad2(h) + ':' + pad2(m) + ':' + pad2(sec);
    }

    function formatElapsed(ms) {
        var mins = Math.floor(ms / 60000);
        if (mins < 1) {
            return 'Less than 1 min';
        }
        var h = Math.floor(mins / 60);
        var m = mins % 60;
        if (h < 1) {
            return m + ' min';
        }
        return h + (h === 1 ? ' hr' : ' hrs') + (m ? ' ' + m + ' min' : '');
    }

    function startCountdown(rental) {
        stopCountdown();
        updateCountdown(rental);
        countdownTimer = setInterval(function () {
            updateCountdown(rental);
        }, 1000);
    }

    function stopCountdown() {
        if (countdownTimer !== null) {
            clearInterval(countdownTimer);
            countdownTimer = null;
        }
        if (overdueTimer !== null) {
            clearInterval(overdueTimer);
            overdueTimer = null;
        }
    }

    // Remaining time, progress bar and elapsed/total labels all come from the same
    // start (handover) and expected-return timestamps, so they always agree.
    function updateCountdown(rental) {
        var start = parseIsoDate(rental.HandoverDate);
        var end = parseIsoDate(rental.ExpectedReturnDate);
        if (!start || !end) {
            return;
        }

        var total = end.getTime() - start.getTime();
        var now = new Date().getTime();
        var elapsed = Math.min(Math.max(now - start.getTime(), 0), total);
        var remaining = Math.max(end.getTime() - Math.max(now, start.getTime()), 0);
        var percent = total > 0 ? Math.floor((elapsed / total) * 100) : 100;

        var display = document.getElementById('ongoingCountdownDisplay');
        display.textContent = formatClock(remaining);
        if (remaining <= 0) {
            display.classList.add('is-complete');
        } else {
            display.classList.remove('is-complete');
        }

        document.getElementById('ongoingProgressFill').style.width = (total > 0 ? (elapsed / total) * 100 : 100).toFixed(1) + '%';
        document.getElementById('ongoingProgressBar').setAttribute('aria-valuenow', String(percent));
        setText('ongoingElapsedLabel', formatElapsed(elapsed) + ' elapsed');
        setText('ongoingTotalLabel', percent + '% of ' + rental.Duration);
    }

    // -------------------------------------------------------------------------
    // Shared helpers for the Overdue / Return / Dispute modals
    // -------------------------------------------------------------------------
    function formatPeso(n) {
        var value = Number(n) || 0;
        return '\u20B1' + value.toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function formatTimeOfDay(d) {
        var h = d.getHours();
        var suffix = h >= 12 ? 'PM' : 'AM';
        h = h % 12;
        if (h === 0) {
            h = 12;
        }
        return h + ':' + pad2(d.getMinutes()) + ' ' + suffix;
    }

    // "1 hr 35 min", "2 hrs", "45 min"
    function formatSpan(ms) {
        var mins = Math.max(0, Math.floor(ms / 60000));
        var h = Math.floor(mins / 60);
        var m = mins % 60;
        if (h < 1) {
            return m + ' min';
        }
        return h + (h === 1 ? ' hr' : ' hrs') + (m ? ' ' + m + ' min' : '');
    }

    function ownerLine(rental) {
        if (!rental.OwnerName) {
            return '';
        }
        return 'Owner: ' + rental.OwnerName + (rental.OwnerProgram ? ' \u2022 ' + rental.OwnerProgram : '');
    }

    // Fills the item card + borrower row that the Overdue / Return modals share.
    function fillRentalSummary(prefix, rental) {
        setText(prefix + 'RequestId', rental.RequestId);
        setText(prefix + 'ItemCategory', rental.Category);
        setText(prefix + 'ItemName', rental.ItemName);
        setText(prefix + 'Owner', ownerLine(rental));
        setText(prefix + 'Location', rental.ItemLocation);
        setThumb(prefix + 'ItemThumb', rental.ItemImageUrl, rental.ItemName);
        setText(prefix + 'HourlyRate', rental.HourlyRateFormatted);
        setText(prefix + 'BorrowerInitials', rental.BorrowerInitials);
        setText(prefix + 'BorrowerName', rental.BorrowerName);
        setText(prefix + 'BorrowerProgram', rental.BorrowerProgram);
        setText(prefix + 'HandoverDate', rental.HandoverDateFormatted);
        setText(prefix + 'HandoverTime', rental.HandoverTimeFormatted);
        setText(prefix + 'Duration', rental.Duration);
        setText(prefix + 'ExpectedReturn', rental.ExpectedReturnFormatted);
    }

    // Item card + borrower card + Requested Rental Details, shared by the Accepted / Declined /
    // Disputed / Completed modals (same markup and styles as the other status modals).
    function fillStatusModal(prefix, rental) {
        fillRentalSummary(prefix, rental);
        setText(prefix + 'HandoverLocation', rental.HandoverLocation);
        setText(prefix + 'FeeBreakdown', rental.RentalFeeFormatted);
        if (prefix === 'accepted') setText('acceptedRequestNote', rental.RequestNote || 'No note was included with the request.');
    }

    // Single source of truth for the late fee, used by the Overdue modal and the Return modal:
    //   overdue time    = now - expected return
    //   billable time   = overdue time - grace period (never below 0)
    //   late hours      = billable time rounded UP to the next started hour
    //   late fee        = late hours x hourly rate
    function computeLateFee(rental, nowMs) {
        var end = parseIsoDate(rental.ExpectedReturnDate);
        var graceMin = Number(rental.GracePeriodMinutes) || 0;
        var rate = Number(rental.HourlyRate) || 0;
        var endMs = end ? end.getTime() : nowMs;
        var overdueMs = Math.max(0, nowMs - endMs);
        var billableMs = Math.max(0, overdueMs - graceMin * 60000);
        var lateHours = Math.ceil(billableMs / 3600000);
        return {
            expectedReturn: end,
            overdueMs: overdueMs,
            graceMinutes: graceMin,
            graceEnd: new Date(endMs + graceMin * 60000),
            billableMs: billableMs,
            lateHours: lateHours,
            rate: rate,
            fee: Math.round(lateHours * rate * 100) / 100
        };
    }

    // --- Overdue ---
    function openOverdueModal(rental, triggerEl) {
        flow.rental = rental;
        flow.trigger = triggerEl || null;
        flow.returnTo = null;
        flow.returnFocus = null;

        fillRentalSummary('overdue', rental);
        setText('overdueBaseFee', rental.RentalFeeFormatted);

        showFlowModal(elements.overdueModal, elements.btnCloseOverdueModal);
    }

    function startOverdueTimer(rental) {
        stopCountdown();
        updateOverdue(rental);
        overdueTimer = setInterval(function () {
            updateOverdue(rental);
        }, 1000);
    }

    function updateOverdue(rental) {
        var start = parseIsoDate(rental.HandoverDate);
        var end = parseIsoDate(rental.ExpectedReturnDate);
        if (!start || !end) {
            return;
        }

        var calc = computeLateFee(rental, new Date().getTime());
        var rentalMs = end.getTime() - start.getTime();
        var perHour = formatPeso(calc.rate) + ' / hr';

        setText('overdueTimeDisplay', formatClock(calc.overdueMs));
        setText('overdueStartedLabel', 'Since ' + rental.ExpectedReturnFormatted);
        setText('overdueLateFee', formatPeso(calc.fee));
        setText('overdueLateFeeBreakdown', calc.lateHours > 0
            ? calc.lateHours + (calc.lateHours === 1 ? ' late hr' : ' late hrs') + ' \u00D7 ' + perHour
            : 'Still within the grace period');

        // Calculation breakdown
        setText('overdueBdGrace', calc.graceMinutes + ' minutes (until ' + formatTimeOfDay(calc.graceEnd) + ')');
        setText('overdueBdRate', perHour);
        setText('overdueBdBillable', calc.billableMs > 0 ? formatSpan(calc.billableMs) + ' after the grace period' : 'None yet (within the grace period)');
        setText('overdueBdHours', calc.lateHours + (calc.lateHours === 1 ? ' hr' : ' hrs') + ' (rounded up to the next started hour)');
        setText('overdueBdTotal', calc.lateHours + ' \u00D7 ' + formatPeso(calc.rate) + ' = ' + formatPeso(calc.fee));

        // Progress: the whole rental period has elapsed, followed by the overdue time
        var overall = rentalMs + calc.overdueMs;
        var rentalShare = overall > 0 ? (rentalMs / overall) * 100 : 100;
        document.getElementById('overdueBarRental').style.width = rentalShare.toFixed(1) + '%';
        document.getElementById('overdueBarLate').style.width = (100 - rentalShare).toFixed(1) + '%';
        setText('overdueBarLeftLabel', '100% of rental period elapsed (' + rental.Duration + ')');
        setText('overdueBarRightLabel', '+' + formatSpan(calc.overdueMs) + ' overdue');
    }

    // --- Returned ---
    // Reached from the table (Returned rows) and right after Ongoing / Overdue -> Mark as Returned.
    function openReturnModal(rental, triggerEl) {
        if (!rental) {
            return;
        }
        if (flow.rental !== rental) {
            flow.rental = rental;
            flow.trigger = triggerEl || null;
        }
        flow.returnTo = null;
        flow.returnFocus = null;

        fillRentalSummary('return', rental);
        setText('returnFeeBreakdown', rental.RentalFeeFormatted);
        setText('returnReturnedOn', rental.ReturnedAtFormatted);
        setText('returnTiming', rental.ReturnOverdueMs > 0
            ? formatSpan(rental.ReturnOverdueMs) + ' after the expected return'
            : 'On time');

        // A rental that came back overdue also shows what the delay cost
        var lateRow = document.getElementById('returnLateRow');
        if (rental.WasOverdue) {
            setText('returnLateText', formatSpan(rental.ReturnOverdueMs) + ' past the expected return');
            setText('returnLateFee', formatPeso(rental.LateFee));
            lateRow.style.display = '';
        } else {
            lateRow.style.display = 'none';
        }

        showFlowModal(elements.returnModal, elements.btnCloseReturnModal);
    }

    // --- Shared confirmation step (Mark as Completed / Mark as Resolved) ---
    var CONFIRM_STEPS = {
        completed: {
            title: 'Mark as Completed?',
            text: 'Confirm that the item came back in good condition and the rental is finished.',
            result: 'The rental moves to Completed and the booking is closed.',
            ok: '\u2713 Mark as Completed',
            from: ['Returned']
        },
        resolved: {
            title: 'Mark as Resolved?',
            text: 'Confirm that you and the borrower have settled the reported issue.',
            result: 'The dispute is closed and the rental moves to Completed.',
            ok: '\u2713 Mark as Resolved',
            from: ['Disputed']
        }
    };
    var confirmKind = null;

    function openStatusConfirm(kind, returnToModal, returnFocusEl) {
        var rental = flow.rental;
        var step = CONFIRM_STEPS[kind];
        if (!rental || !step || step.from.indexOf(rental.Status) === -1) {
            return;
        }

        confirmKind = kind;
        setText('statusConfirmTitle', step.title);
        setText('statusConfirmText', step.text);
        setText('statusConfirmResult', step.result);
        setText('statusConfirmRequestId', rental.RequestId);
        setText('statusConfirmItemName', rental.ItemName);
        setText('statusConfirmBorrower', 'Borrower: ' + rental.BorrowerName);
        setThumb('statusConfirmThumb', rental.ItemImageUrl, rental.ItemName);
        elements.btnStatusConfirmOk.textContent = step.ok;

        flow.returnTo = returnToModal || null;
        flow.returnFocus = returnFocusEl || null;
        showFlowModal(elements.statusConfirmModal, elements.btnStatusConfirmCancel);
    }

    function runStatusConfirm() {
        var rental = flow.rental;
        var step = CONFIRM_STEPS[confirmKind];
        // Ignore a second click (or a stale dialog): the record must still be in the expected status
        if (!rental || !step || step.from.indexOf(rental.Status) === -1) {
            return;
        }

        if (confirmKind === 'completed') {
            postRentalAction('data-return-url', rental.BookingObjectId, function (result) {
                if (!result || !result.ok) { window.alert(result && result.message ? result.message : 'Return confirmation could not be saved.'); return; }
                window.location.reload();
            });
        } else if (confirmKind === 'resolved') {
            window.alert('Only an administrator can resolve a dispute. Use Dispute Management to review and close it.');
        }
    }

    // Returned -> Completed (Mark as Completed) and Disputed -> Completed (Mark as Resolved)
    function markCompleted(rental, fromDispute) {
        var requestId = rental.RequestId;
        rental.CompletedAtFormatted = formatDateTime(new Date());
        if (fromDispute) {
            rental.DisputeResolved = true;
        }
        rental.Status = 'Completed';
        updateSummaryCards();
        confirmKind = null;
        endFlow(false);
        applyAllFiltersAndPaginate();
        focusViewButton(requestId);
    }

    // --- Dispute ---
    function getDisputeRadios() {
        return elements.disputeCategories.querySelectorAll('input[name="disputeCategory"]');
    }

    function syncDisputeTiles() {
        var radios = getDisputeRadios();
        for (var i = 0; i < radios.length; i++) {
            var tile = radios[i].parentNode;
            if (radios[i].checked) {
                tile.classList.add('is-selected');
            } else {
                tile.classList.remove('is-selected');
            }
        }
    }

    function releaseDisputeImages() {
        for (var i = 0; i < disputeImages.length; i++) {
            if (window.URL && URL.revokeObjectURL) {
                URL.revokeObjectURL(disputeImages[i].url);
            }
        }
        disputeImages = [];
    }

    function resetDisputeForm() {
        var radios = getDisputeRadios();
        for (var i = 0; i < radios.length; i++) {
            radios[i].checked = false;
        }
        syncDisputeTiles();
        elements.disputeDescription.value = '';
        elements.disputeDescription.classList.remove('is-invalid');
        elements.disputeCategoryError.style.display = 'none';
        elements.disputeDescError.style.display = 'none';
        showDisputeImageError('');
        releaseDisputeImages();
        elements.disputeImageInput.value = '';
        renderDisputeImages();
    }

    function openDisputeModal(returnToModal, returnFocusEl) {
        var rental = flow.rental;
        if (!rental || (rental.Status !== 'Ongoing' && rental.Status !== 'Overdue' && rental.Status !== 'Returned')) {
            return;
        }

        setText('disputeRequestId', rental.RequestId);
        setText('disputeItemCategory', rental.Category);
        setText('disputeItemName', rental.ItemName);
        setText('disputeBorrower', ownerLine(rental) + '  |  Borrower: ' + rental.BorrowerName);
        setText('disputeSchedule', rental.HandoverScheduleFormatted + '  \u2192  ' + rental.ExpectedReturnFormatted);
        setThumb('disputeItemThumb', rental.ItemImageUrl, rental.ItemName);
        resetDisputeForm();

        flow.returnTo = returnToModal || null;
        flow.returnFocus = returnFocusEl || null;
        showFlowModal(elements.disputeModal, getDisputeRadios()[0]);
    }

    function showDisputeImageError(message) {
        elements.disputeImageError.textContent = message || '';
        elements.disputeImageError.style.display = message ? 'block' : 'none';
    }

    function formatFileSize(bytes) {
        if (bytes >= 1024 * 1024) {
            return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
        }
        return Math.max(1, Math.round(bytes / 1024)) + ' KB';
    }

    function handleDisputeFiles(fileList) {
        var files = Array.prototype.slice.call(fileList || []);
        var problems = [];
        var overLimit = false;

        for (var i = 0; i < files.length; i++) {
            var f = files[i];
            if (!/^image\//i.test(f.type)) {
                problems.push('"' + f.name + '" is not an image.');
            } else if (f.size > MAX_IMAGE_BYTES) {
                problems.push('"' + f.name + '" is larger than 5 MB.');
            } else if (disputeImages.length >= MAX_DISPUTE_IMAGES) {
                overLimit = true;
            } else {
                disputeImages.push({ file: f, url: URL.createObjectURL(f) });
            }
        }

        if (overLimit) {
            problems.unshift('You can upload a maximum of ' + MAX_DISPUTE_IMAGES + ' images.');
        }
        showDisputeImageError(problems.join(' '));
        renderDisputeImages();
    }

    function removeDisputeImage(index) {
        if (isNaN(index) || index < 0 || index >= disputeImages.length) {
            return;
        }
        if (window.URL && URL.revokeObjectURL) {
            URL.revokeObjectURL(disputeImages[index].url);
        }
        disputeImages.splice(index, 1);
        showDisputeImageError('');
        renderDisputeImages();
        if (disputeImages.length < MAX_DISPUTE_IMAGES) {
            elements.btnDisputeUpload.focus();
        }
    }

    // Built with DOM methods (not innerHTML) because file names are user supplied
    function renderDisputeImages() {
        var box = elements.disputeImagePreviews;
        box.innerHTML = '';

        for (var i = 0; i < disputeImages.length; i++) {
            var item = disputeImages[i];

            var card = document.createElement('div');
            card.className = 'rm-image-preview';

            var thumb = document.createElement('div');
            thumb.className = 'rm-image-preview-item';
            var img = document.createElement('img');
            img.src = item.url;
            img.alt = 'Evidence image ' + (i + 1) + ': ' + item.file.name;
            thumb.appendChild(img);

            var remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'rm-image-preview-remove';
            remove.setAttribute('data-index', String(i));
            remove.setAttribute('aria-label', 'Remove ' + item.file.name);
            remove.innerHTML = '&times;';
            thumb.appendChild(remove);

            var name = document.createElement('span');
            name.className = 'rm-image-preview-name';
            name.title = item.file.name;
            name.textContent = item.file.name;

            var size = document.createElement('span');
            size.className = 'rm-image-preview-size';
            size.textContent = formatFileSize(item.file.size);

            card.appendChild(thumb);
            card.appendChild(name);
            card.appendChild(size);
            box.appendChild(card);
        }

        var count = disputeImages.length;
        var full = count >= MAX_DISPUTE_IMAGES;
        elements.disputeImageCount.textContent = count + '/' + MAX_DISPUTE_IMAGES + ' images added';
        elements.btnDisputeUpload.disabled = full;
        if (full) {
            elements.btnDisputeUpload.classList.add('is-full');
        } else {
            elements.btnDisputeUpload.classList.remove('is-full');
        }
        elements.disputeUploadTitle.textContent = full ? 'Limit reached (' + count + '/' + MAX_DISPUTE_IMAGES + ')' : 'Click to upload images';
        elements.disputeUploadSub.textContent = full ? 'Remove an image to upload another' : count + ' of ' + MAX_DISPUTE_IMAGES + ' added';
    }

    function submitDispute() {
        var rental = flow.rental;
        if (!rental || (rental.Status !== 'Ongoing' && rental.Status !== 'Overdue' && rental.Status !== 'Returned')) {
            return;
        }

        var chosen = null;
        var radios = getDisputeRadios();
        for (var i = 0; i < radios.length; i++) {
            if (radios[i].checked) {
                chosen = radios[i];
                break;
            }
        }
        var description = elements.disputeDescription.value.replace(/^\s+|\s+$/g, '');

        var firstInvalid = null;
        if (!chosen) {
            elements.disputeCategoryError.style.display = 'block';
            firstInvalid = radios[0];
        }
        if (!description) {
            elements.disputeDescError.style.display = 'block';
            elements.disputeDescription.classList.add('is-invalid');
            firstInvalid = firstInvalid || elements.disputeDescription;
        }
        if (disputeImages.length > MAX_DISPUTE_IMAGES) {
            showDisputeImageError('You can upload a maximum of ' + MAX_DISPUTE_IMAGES + ' images.');
            firstInvalid = firstInvalid || elements.btnDisputeUpload;
        }
        if (firstInvalid) {
            firstInvalid.focus();
            return;
        }

        var root = document.querySelector('.my-rentals-page');
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var form = new FormData();
        form.append('id', rental.BookingObjectId || '');
        form.append('reason', chosen.value);
        form.append('details', description);
        if (token) form.append('__RequestVerificationToken', token.value);
        disputeImages.forEach(function (entry) { form.append('evidenceFiles', entry.file, entry.file.name); });
        elements.btnDisputeSubmit.disabled = true;
        fetch(root.getAttribute('data-dispute-url'), { method: 'POST', credentials: 'same-origin', body: form })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                elements.btnDisputeSubmit.disabled = false;
                if (!result || !result.ok) { window.alert(result && result.message ? result.message : 'The dispute could not be submitted.'); return; }
                window.location.reload();
            })
            .catch(function () { elements.btnDisputeSubmit.disabled = false; window.alert('The dispute could not be saved. Refresh and try again.'); });
    }

    function bindFlowModalEvents() {
        // Pending details
        elements.btnClosePendingDetails.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnPendingAccept.addEventListener('click', function (e) {
            e.preventDefault();
            openAcceptModal();
        });
        elements.btnPendingDecline.addEventListener('click', function (e) {
            e.preventDefault();
            openDeclineModal();
        });

        // Accept
        elements.btnAcceptCancel.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnAcceptConfirm.addEventListener('click', function (e) {
            e.preventDefault();
            confirmAccept();
        });

        // Decline
        elements.btnCloseDeclineModal.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnDeclineCancel.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnDeclineConfirm.addEventListener('click', function (e) {
            e.preventDefault();
            confirmDecline();
        });
        var radios = getDeclineRadios();
        for (var r = 0; r < radios.length; r++) {
            radios[r].addEventListener('change', function () {
                elements.declineReasonError.style.display = 'none';
                syncDeclineLabels();
            });
        }

        // Ongoing
        elements.btnCloseOngoingModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnOngoingClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });

        // Overdue
        elements.btnCloseOverdueModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnOverdueClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });

        // Return
        elements.btnCloseReturnModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnReturnClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnReturnReportDispute.addEventListener('click', function (e) {
            e.preventDefault();
            openDisputeModal(elements.returnModal, elements.btnReturnReportDispute);
        });
        elements.btnReturnConfirm.addEventListener('click', function (e) {
            e.preventDefault();
            openStatusConfirm('completed', elements.returnModal, elements.btnReturnConfirm);
        });

        // Accepted
        elements.btnCloseAcceptedModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnAcceptedClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnAcceptedConfirmHandover.addEventListener('click', function (e) {
            e.preventDefault();
            confirmHandover();
        });

        // Declined (view only)
        elements.btnCloseDeclinedModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnDeclinedClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });

        // Disputed
        elements.btnCloseDisputedModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnDisputedClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        if (elements.btnDisputedResolve) elements.btnDisputedResolve.addEventListener('click', function (e) {
            e.preventDefault();
            openStatusConfirm('resolved', elements.disputedModal, elements.btnDisputedResolve);
        });

        // Completed (view only)
        elements.btnCloseCompletedModal.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });
        elements.btnCompletedClose.addEventListener('click', function (e) {
            e.preventDefault();
            endFlow(true);
        });

        // Shared confirmation step
        elements.btnStatusConfirmCancel.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnStatusConfirmOk.addEventListener('click', function (e) {
            e.preventDefault();
            runStatusConfirm();
        });

        // Dispute
        elements.btnCloseDisputeModal.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnDisputeCancel.addEventListener('click', function (e) {
            e.preventDefault();
            cancelConfirmation();
        });
        elements.btnDisputeSubmit.addEventListener('click', function (e) {
            e.preventDefault();
            submitDispute();
        });
        elements.disputeCategories.addEventListener('change', function () {
            syncDisputeTiles();
            elements.disputeCategoryError.style.display = 'none';
        });
        elements.disputeDescription.addEventListener('input', function () {
            if (elements.disputeDescription.value.replace(/^\s+|\s+$/g, '')) {
                elements.disputeDescError.style.display = 'none';
                elements.disputeDescription.classList.remove('is-invalid');
            }
        });
        elements.btnDisputeUpload.addEventListener('click', function (e) {
            e.preventDefault();
            if (disputeImages.length < MAX_DISPUTE_IMAGES) {
                elements.disputeImageInput.click();
            }
        });
        elements.disputeImageInput.addEventListener('change', function () {
            handleDisputeFiles(this.files);
            this.value = '';
        });
        elements.disputeImagePreviews.addEventListener('click', function (e) {
            var btn = findAncestorWithClass(e.target, 'rm-image-preview-remove', elements.disputeImagePreviews);
            if (btn) {
                e.preventDefault();
                removeDisputeImage(parseInt(btn.getAttribute('data-index'), 10));
            }
        });
        window.addEventListener('beforeunload', function () {
            releaseDisputeImages();
        });

        // Click on the dark backdrop dismisses a modal. The mouse must go down AND up on the
        // backdrop, so selecting text inside the dialog and releasing outside does not close it.
        var modals = getFlowModals();
        for (var i = 0; i < modals.length; i++) {
            (function (modalEl) {
                var downOnBackdrop = false;
                modalEl.addEventListener('mousedown', function (e) {
                    downOnBackdrop = (e.target === modalEl);
                });
                modalEl.addEventListener('click', function (e) {
                    // The Dispute form holds typed text and images, so a stray backdrop click must not discard it
                    if (e.target === modalEl && downOnBackdrop && modalEl !== elements.disputeModal) {
                        dismissFlowModal(modalEl);
                    }
                    downOnBackdrop = false;
                });
            })(modals[i]);
        }
    }

    // -------------------------------------------------------------------------
    // Refresh: reload the data and put the page back to its initial state
    // -------------------------------------------------------------------------
    function refreshRentals() {
        if (isRefreshing) {
            return;
        }
        isRefreshing = true;
        elements.btnRefresh.classList.add('is-loading');
        elements.btnRefresh.setAttribute('aria-busy', 'true');

        var startedAt = new Date().getTime();

        fetchLatestPage(function (doc) {
            // Keep the spinner visible long enough to be noticed
            var wait = Math.max(0, 450 - (new Date().getTime() - startedAt));
            setTimeout(function () {
                if (doc) {
                    applyFetchedPage(doc);
                }
                resetToInitialState();
                elements.btnRefresh.classList.remove('is-loading');
                elements.btnRefresh.removeAttribute('aria-busy');
                isRefreshing = false;
            }, wait);
        });
    }

    // Re-requests this same page from the server and hands back the parsed document.
    // Calls back with null when the request or parsing fails (the page still resets).
    function fetchLatestPage(callback) {
        var done = false;
        function finish(doc) {
            if (!done) {
                done = true;
                callback(doc);
            }
        }

        try {
            var url = window.location.pathname + window.location.search;
            url += (url.indexOf('?') === -1 ? '?' : '&') + '_=' + new Date().getTime();

            var xhr = new XMLHttpRequest();
            xhr.open('GET', url, true);
            xhr.onreadystatechange = function () {
                if (xhr.readyState !== 4) {
                    return;
                }
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        finish(new DOMParser().parseFromString(xhr.responseText, 'text/html'));
                    } catch (parseErr) {
                        finish(null);
                    }
                } else {
                    finish(null);
                }
            };
            xhr.send();
        } catch (err) {
            finish(null);
        }
    }

    function applyFetchedPage(doc) {
        var rawEl = doc.getElementById('myRentalsRawData');
        if (!rawEl || !rawEl.textContent) {
            return;
        }

        var list;
        try {
            list = JSON.parse(rawEl.textContent.trim());
        } catch (err) {
            return;
        }
        if (Object.prototype.toString.call(list) !== '[object Array]') {
            return;
        }
        state.allRentals = list;

        // Keep the four overview numbers in sync with the fresh response
        updateSummaryCards();
    }

    function resetToInitialState() {
        hideDetailsModal();
        detailsTrigger = null;
        endFlow(false);
        hideFilterModal();

        state.appliedStatuses = DEFAULT_STATUSES.slice();
        state.appliedDate = 'All Time';
        state.appliedSearch = '';
        state.currentPage = 1;
        confirmKind = null;

        elements.searchInput.value = '';
        elements.searchClear.style.display = 'none';
        elements.dateFilter.value = 'All Time';

        syncModalCheckboxes(state.appliedStatuses);
        updateModalCount();
        updateFilterBadge();
        updateSummaryCards();
        applyAllFiltersAndPaginate();
    }

    function scrollToTable() {
        var container = document.querySelector('.my-rentals-table-section');
        if (container && typeof container.scrollIntoView === 'function') {
            container.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

