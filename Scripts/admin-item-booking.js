(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var page = document.querySelector(".aib-page");
        if (!page) return;

        var endpoint = page.dataset.endpoint;
        if (!endpoint) {
            console.error("Missing data-endpoint on .aib-page");
            return;
        }

        // Filter Elements
        var searchInput = document.getElementById("aibSearchInput");
        var categorySelect = document.getElementById("aibCategorySelect");
        var statusSelect = document.getElementById("aibStatusSelect");
        var availabilitySelect = document.getElementById("aibAvailabilitySelect");
        var btnResetFilters = document.getElementById("btnResetFilters");

        var tableBody = document.getElementById("aibTableBody");
        var paginationInfoDisplay = page.querySelector("[data-aib-pagination-info]");
        var selectedCountDisplay = page.querySelector("[data-aib-selected-count]");

        // KPI Elements
        var kpiPending = page.querySelector("[data-kpi-pending]");
        var kpiActive = page.querySelector("[data-kpi-active]");
        var kpiInactive = page.querySelector("[data-kpi-inactive]");
        var kpiTotal = page.querySelector("[data-kpi-total]");

        // Modal Elements
        var modalOverlay = document.getElementById("aibModalOverlay");
        var modalContainer = document.getElementById("aibModalContainer");
        var btnCloseModal = document.getElementById("btnAibCloseModal");

        // Dossier Fields
        var dossierId = page.querySelector("[data-dossier-id]");
        var dossierStatusPill = page.querySelector("[data-dossier-status-pill]");
        var dossierName = page.querySelector("[data-dossier-name]");
        var dossierCategory = page.querySelector("[data-dossier-category]");
        var dossierIdChip = page.querySelector("[data-dossier-id-chip]");
        var dossierOwner = page.querySelector("[data-dossier-owner]");
        var dossierPrice = page.querySelector("[data-dossier-price]");
        var dossierMeta = page.querySelector("[data-dossier-meta]");
        var dossierDesc = page.querySelector("[data-dossier-desc]");
        var dossierCourse = page.querySelector("[data-dossier-course]");
        var dossierCondition = page.querySelector("[data-dossier-condition]");
        var dossierHandover = page.querySelector("[data-dossier-handover]");
        var dossierReturn = page.querySelector("[data-dossier-return]");
        var dossierDays = page.querySelector("[data-dossier-days]");
        var dossierActivePill = page.querySelector("[data-dossier-active-pill]");

        var btnApprove = page.querySelector('[data-aib-action="approve"]');
        var btnReject = page.querySelector('[data-aib-action="reject"]');

        var statusConfig = {
            pending: { label: "Pending", className: "aib-pill aib-pill--pending" },
            approved: { label: "Approved", className: "aib-pill aib-pill--approved" },
            rejected: { label: "Rejected", className: "aib-pill aib-pill--rejected" }
        };

        var activeConfig = {
            active: { label: "Active", className: "aib-pill aib-pill--active" },
            inactive: { label: "Inactive", className: "aib-pill aib-pill--inactive" }
        };

        var daysOfWeek = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        // Helper: Escape HTML
        function esc(str) {
            return (str || "").toString()
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        // Build Table Row
        function buildRow(item) {
            var status = (item.status || "pending").toLowerCase();
            var active = (item.active || "inactive").toLowerCase();
            var sConfig = statusConfig[status] || statusConfig.pending;
            var aConfig = activeConfig[active] || activeConfig.inactive;

            var thumbClass = item.category === "Electronics" ? "aib-item-thumb--indigo"
                : item.category === "School Supplies" ? "aib-item-thumb--purple"
                    : item.category === "Other Items" ? "aib-item-thumb--amber"
                        : "aib-item-thumb--blue";

            var deleteBtn = (status === "approved" && active === "active")
                ? '<button type="button" class="aib-btn-delete" data-delete-id="' + esc(item.id) + '">Delete</button>'
                : "";

            return '' +
                '<tr class="clickable-row" data-aib-row data-id="' + esc(item.id) + '" data-name="' + esc(item.name) + '" data-category="' + esc(item.category) + '" data-owner="' + esc(item.owner) + '" data-status="' + status + '" data-active="' + active + '" data-price="' + esc(item.price) + '" data-meta="' + esc(item.meta) + '" data-desc="' + esc(item.desc) + '" data-course="' + esc(item.course) + '" data-condition="' + esc(item.condition) + '" data-handover="' + esc(item.handover) + '" data-return="' + esc(item.returnTime) + '" data-days="' + esc(item.days) + '">' +
                '<td class="aib-td--id">' + esc(item.id) + '</td>' +
                '<td>' +
                '<div class="aib-item-cell">' +
                '<div class="aib-item-thumb ' + thumbClass + '" aria-hidden="true">' +
                '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>' +
                '</div>' +
                '<div class="aib-item-cell__details">' +
                '<div class="aib-item-cell__name">' + esc(item.name) + '</div>' +
                '<div class="aib-item-cell__desc">' + esc(item.meta.split("•")[0]) + '</div>' +
                '</div>' +
                '</div>' +
                '</td>' +
                '<td class="aib-td--muted">' + esc(item.category) + '</td>' +
                '<td class="aib-td--muted">' + esc(item.owner) + '</td>' +
                '<td class="aib-td--muted">' + esc(item.price) + '</td>' +
                '<td><span class="' + sConfig.className + '">' + sConfig.label + '</span></td>' +
                '<td><span class="' + aConfig.className + '">' + aConfig.label + '</span></td>' +
                '<td class="aib-td--right">' +
                '<button type="button" class="aib-btn-open-dossier">Inspect</button>' +
                deleteBtn +
                '</td>' +
                '</tr>';
        }

        // Render whole table
        function renderTable(items) {
            if (!tableBody) return;
            tableBody.innerHTML = items.map(buildRow).join("");
            wireRowEvents();
        }

        // Update KPIs
        function updateKpis(items) {
            var pending = 0, active = 0, inactive = 0;
            items.forEach(function (i) {
                if ((i.status || "").toLowerCase() === "pending") pending++;
                if ((i.active || "").toLowerCase() === "active") active++;
                else inactive++;
            });
            if (kpiPending) kpiPending.textContent = pending;
            if (kpiActive) kpiActive.textContent = active;
            if (kpiInactive) kpiInactive.textContent = inactive;
            if (kpiTotal) kpiTotal.textContent = items.length;
        }

        // Filters
        function applyFilters() {
            var searchTerm = (searchInput ? searchInput.value : "").trim().toLowerCase();
            var selectedCat = categorySelect ? categorySelect.value : "all";
            var selectedStatus = statusSelect ? statusSelect.value.toLowerCase() : "all";
            var selectedAvail = availabilitySelect ? availabilitySelect.value.toLowerCase() : "all";

            var visibleCount = 0;
            var rows = page.querySelectorAll("[data-aib-row]");

            rows.forEach(function (row) {
                var rowCat = row.dataset.category || "";
                var rowStatus = (row.dataset.status || "").toLowerCase();
                var rowActive = (row.dataset.active || "").toLowerCase();
                var textContent = row.textContent.toLowerCase();

                var matchesCat = selectedCat === "all" || rowCat === selectedCat;
                var matchesStatus = selectedStatus === "all" || rowStatus === selectedStatus;
                var matchesAvail = selectedAvail === "all" || rowActive === selectedAvail;
                var matchesSearch = !searchTerm || textContent.indexOf(searchTerm) > -1;

                if (matchesCat && matchesStatus && matchesAvail && matchesSearch) {
                    row.style.display = "";
                    visibleCount++;
                } else {
                    row.style.display = "none";
                }
            });

            if (paginationInfoDisplay) {
                paginationInfoDisplay.textContent = "Showing 1–" + visibleCount + " of " + visibleCount + " submissions";
            }
            if (selectedCountDisplay) {
                selectedCountDisplay.textContent = "Showing " + visibleCount + " Records";
            }
        }

        if (searchInput) searchInput.addEventListener("input", applyFilters);
        if (categorySelect) categorySelect.addEventListener("change", applyFilters);
        if (statusSelect) statusSelect.addEventListener("change", applyFilters);
        if (availabilitySelect) availabilitySelect.addEventListener("change", applyFilters);

        if (btnResetFilters) {
            btnResetFilters.addEventListener("click", function (e) {
                e.preventDefault();
                if (searchInput) searchInput.value = "";
                if (categorySelect) categorySelect.value = "all";
                if (statusSelect) statusSelect.value = "all";
                if (availabilitySelect) availabilitySelect.value = "all";
                applyFilters();
            });
        }

        // Dossier
        function renderDays(daysString) {
            if (!dossierDays) return;
            dossierDays.innerHTML = "";
            var activeDays = (daysString || "").split(",").map(function (d) { return d.trim(); });
            daysOfWeek.forEach(function (day) {
                var span = document.createElement("span");
                span.className = "aib-readonly-day";
                if (activeDays.indexOf(day) > -1) span.classList.add("is-active");
                span.textContent = day;
                dossierDays.appendChild(span);
            });
        }

        function updateActionButtons(status) {
            var isPending = status === "pending";
            if (btnApprove) btnApprove.style.display = isPending ? "" : "none";
            if (btnReject) btnReject.style.display = isPending ? "" : "none";
        }

        function populateDossier(row) {
            if (!row) return;
            var ds = row.dataset;
            var currentStatus = (ds.status || "pending").toLowerCase();
            var currentActive = (ds.active || "inactive").toLowerCase();

            if (dossierId) dossierId.textContent = "#" + (ds.id || "");
            if (dossierName) dossierName.textContent = ds.name || "";
            if (dossierCategory) dossierCategory.textContent = ds.category || "";
            if (dossierIdChip) dossierIdChip.textContent = "#" + (ds.id || "");
            if (dossierOwner) dossierOwner.textContent = "Owner: " + (ds.owner || "");
            if (dossierPrice) dossierPrice.textContent = ds.price || "";
            if (dossierMeta) dossierMeta.textContent = ds.meta || "";
            if (dossierDesc) dossierDesc.textContent = ds.desc || "";
            if (dossierCourse) dossierCourse.textContent = ds.course || "";
            if (dossierCondition) dossierCondition.textContent = ds.condition || "";
            if (dossierHandover) dossierHandover.textContent = ds.handover || "";
            if (dossierReturn) dossierReturn.textContent = ds.return || "";

            renderDays(ds.days || "");

            if (dossierStatusPill && statusConfig[currentStatus]) {
                dossierStatusPill.className = statusConfig[currentStatus].className;
                dossierStatusPill.textContent = statusConfig[currentStatus].label;
            }
            if (dossierActivePill && activeConfig[currentActive]) {
                dossierActivePill.className = activeConfig[currentActive].className;
                dossierActivePill.textContent = activeConfig[currentActive].label;
            }

            updateActionButtons(currentStatus);
        }

        function openModal(row) {
            populateDossier(row);
            if (modalOverlay) modalOverlay.style.display = "block";
            if (modalContainer) modalContainer.style.display = "flex";
            document.body.style.overflow = "hidden";
        }

        function closeModal() {
            if (modalOverlay) modalOverlay.style.display = "none";
            if (modalContainer) modalContainer.style.display = "none";
            document.body.style.overflow = "";
        }

        if (btnCloseModal) btnCloseModal.addEventListener("click", closeModal);
        if (modalOverlay) modalOverlay.addEventListener("click", closeModal);
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" || e.keyCode === 27) {
                if (modalContainer && modalContainer.style.display !== "none") closeModal();
            }
        });

        // Row event wiring (called after each render)
        function wireRowEvents() {
            page.querySelectorAll(".aib-btn-open-dossier").forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var row = btn.closest("[data-aib-row]");
                    page.querySelectorAll("[data-aib-row]").forEach(function (r) { r.classList.remove("is-selected"); });
                    if (row) row.classList.add("is-selected");
                    openModal(row);
                });
            });

            page.querySelectorAll("[data-aib-row]").forEach(function (row) {
                row.addEventListener("click", function (e) {
                    if (e.target.closest("button")) return;
                    page.querySelectorAll("[data-aib-row]").forEach(function (r) { r.classList.remove("is-selected"); });
                    row.classList.add("is-selected");
                    openModal(row);
                });
            });

            page.querySelectorAll(".aib-btn-delete").forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var id = btn.dataset.deleteId || "";
                    if (!confirm("Permanently delete listing " + id + "?")) return;

                    // TODO: POST to /AdminItemBooking/Delete
                    // fetch('/AdminItemBooking/Delete', { method: 'POST', body: ... })

                    var row = btn.closest("[data-aib-row]");
                    if (row) row.remove();
                    applyFilters();
                });
            });
        }

        // Update Helpers
        function updateRowStatusPill(row, status) {
            var cells = row.querySelectorAll("td");
            var statusCell = cells[5];
            if (statusCell && statusConfig[status]) {
                statusCell.innerHTML = '<span class="' + statusConfig[status].className + '">' + statusConfig[status].label + '</span>';
            }
        }

        function updateRowActivePill(row, active) {
            var cells = row.querySelectorAll("td");
            var activeCell = cells[6];
            if (activeCell && activeConfig[active]) {
                activeCell.innerHTML = '<span class="' + activeConfig[active].className + '">' + activeConfig[active].label + '</span>';
            }
        }

        // Approve/Reject
        if (btnApprove) {
            btnApprove.addEventListener("click", function () {
                var row = page.querySelector("[data-aib-row].is-selected");
                if (!row) return;
                if ((row.dataset.status || "").toLowerCase() !== "pending") return;
                if (!confirm("Approve this listing?")) return;

                // TODO: POST to /AdminItemBooking/Approve
                row.dataset.status = "approved";
                row.dataset.active = "active";
                updateRowStatusPill(row, "approved");
                updateRowActivePill(row, "active");
                populateDossier(row);
                applyFilters();
            });
        }

        if (btnReject) {
            btnReject.addEventListener("click", function () {
                var row = page.querySelector("[data-aib-row].is-selected");
                if (!row) return;
                if ((row.dataset.status || "").toLowerCase() !== "pending") return;
                if (!confirm("Reject this listing?")) return;

                // TODO: POST to /AdminItemBooking/Reject
                row.dataset.status = "rejected";
                row.dataset.active = "inactive";
                updateRowStatusPill(row, "rejected");
                updateRowActivePill(row, "inactive");
                populateDossier(row);
                applyFilters();
            });
        }

        // Fetch and initialize
        fetch(endpoint, { headers: { "Accept": "application/json" } })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!data || !data.success) {
                    console.error("Failed to load listings:", data);
                    return;
                }
                renderTable(data.data || []);
                updateKpis(data.data || []);
                applyFilters();
            })
            .catch(function (err) {
                console.error("Error fetching listings:", err);
                if (selectedCountDisplay) selectedCountDisplay.textContent = "Error loading records";
            });
    });
})();