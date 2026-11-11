(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var page = document.querySelector(".asv-page");
        if (!page) return;

        var endpoint = page.dataset.endpoint;
        if (!endpoint) return;

        var approveUrl = "/AdminStudentVerification/Approve";
        var rejectUrl = "/AdminStudentVerification/Reject";

        var searchInput = document.getElementById("asvSearchInput");
        var statusSelect = document.getElementById("asvStatusSelect");
        var btnResetFilters = document.getElementById("btnResetFilters");

        var tableBody = document.getElementById("asvTableBody");
        var paginationInfo = page.querySelector("[data-asv-pagination-info]");
        var selectedCount = page.querySelector("[data-asv-selected-count]");

        var kpiTotal = page.querySelector("[data-kpi-total]");
        var kpiPending = page.querySelector("[data-kpi-pending]");
        var kpiApproved = page.querySelector("[data-kpi-approved]");
        var kpiRejected = page.querySelector("[data-kpi-rejected]");

        var modalOverlay = document.getElementById("asvModalOverlay");
        var modalContainer = document.getElementById("asvModalContainer");
        var btnCloseModal = document.getElementById("btnAsvCloseModal");

        var dKyc = page.querySelector("[data-dossier-kyc]");
        var dStatusPill = page.querySelector("[data-dossier-status-pill]");
        var dAvatar = page.querySelector("[data-dossier-avatar]");
        var dName = page.querySelector("[data-dossier-name]");
        var dCourse = page.querySelector("[data-dossier-course]");
        var dChip = page.querySelector("[data-dossier-id-chip]");
        var dCampus = page.querySelector("[data-dossier-campus]");
        var dEmail = page.querySelector("[data-dossier-email]");
        var dPhone = page.querySelector("[data-dossier-phone]");
        var dMeta = page.querySelector("[data-dossier-meta]");
        var dCardName = page.querySelector("[data-dossier-card-name]");
        var dCardCourse = page.querySelector("[data-dossier-card-course]");
        var dCardId = page.querySelector("[data-dossier-card-id]");
        var dCorName = page.querySelector("[data-dossier-cor-name]");
        var dCorId = page.querySelector("[data-dossier-cor-id]");

        var btnApprove = page.querySelector('[data-asv-action="approve"]');
        var btnReject = page.querySelector('[data-asv-action="reject"]');

        var tabs = page.querySelectorAll("[data-asv-tab]");
        var tabPanels = page.querySelectorAll("[data-tab-panel]");

        var utilButtons = page.querySelectorAll("[data-asv-util]");
        var docPreviewCard = page.querySelector(".asv-doc-preview");
        var lightboxOverlay = document.getElementById("asvLightbox");
        var lightboxBody = document.getElementById("asvLightboxBody");
        var btnCloseLightbox = document.getElementById("btnAsvCloseLightbox");

        var previewRotated = false;
        var previewZoomed = false;

        var statusConfig = {
            pending: { label: "Pending", className: "asv-pill asv-pill--pending" },
            approved: { label: "Approved", className: "asv-pill asv-pill--approved" },
            rejected: { label: "Rejected", className: "asv-pill asv-pill--rejected" }
        };

        // -------------------------------------------------------
        // HELPERS
        // -------------------------------------------------------
        function esc(str) {
            return (str || "").toString()
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#39;");
        }

        function postForm(url, data, cb) {
            var body = Object.keys(data).map(function (k) {
                return encodeURIComponent(k) + "=" + encodeURIComponent(data[k]);
            }).join("&");

            fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: body
            })
                .then(function (res) { return res.json(); })
                .then(cb)
                .catch(function (err) { console.error(err); alert("Network error, please try again."); });
        }

        // -------------------------------------------------------
        // TABLE
        // -------------------------------------------------------
        function buildRow(s) {
            var status = (s.status || "pending").toLowerCase();
            var cfg = statusConfig[status] || statusConfig.pending;

            return '' +
                '<tr class="clickable-row" data-asv-row data-id="' + esc(s.id) + '" data-kyc="' + esc(s.kyc) + '" data-name="' + esc(s.name) + '" data-initials="' + esc(s.initials) + '" data-course="' + esc(s.course) + '" data-campus="' + esc(s.campus) + '" data-email="' + esc(s.email) + '" data-phone="' + esc(s.phone) + '" data-date="' + esc(s.date) + '" data-status="' + status + '" data-meta="' + esc(s.meta) + '">' +
                '<td class="asv-td--id">' + esc(s.id) + '</td>' +
                '<td>' +
                '<div class="asv-student-cell">' +
                '<span class="asv-avatar asv-avatar--indigo" aria-hidden="true">' + esc(s.initials) + '</span>' +
                '<div class="asv-student-cell__details">' +
                '<div class="asv-student-cell__name">' + esc(s.name) + '</div>' +
                '<div class="asv-student-cell__course">' + esc((s.course || "").split("•")[0]) + '</div>' +
                '</div>' +
                '</div>' +
                '</td>' +
                '<td class="asv-td--muted">' + esc((s.campus || "").split("(")[0]) + '</td>' +
                '<td class="asv-td--muted">' + esc(s.email) + '</td>' +
                '<td class="asv-td--muted">' + esc(s.date) + '</td>' +
                '<td><span class="' + cfg.className + '">' + cfg.label + '</span></td>' +
                '<td class="asv-td--right"><button type="button" class="asv-btn-open-dossier">Inspect</button></td>' +
                '</tr>';
        }

        function renderTable(students) {
            tableBody.innerHTML = students.map(buildRow).join("");
            wireRowEvents();
        }

        function updateKpis(students) {
            var total = students.length;
            var pending = 0, approved = 0, rejected = 0;
            students.forEach(function (s) {
                var st = (s.status || "").toLowerCase();
                if (st === "pending") pending++;
                else if (st === "approved") approved++;
                else if (st === "rejected") rejected++;
            });
            if (kpiTotal) kpiTotal.textContent = total;
            if (kpiPending) kpiPending.textContent = pending;
            if (kpiApproved) kpiApproved.textContent = approved;
            if (kpiRejected) kpiRejected.textContent = rejected;
        }

        function applyFilters() {
            var searchTerm = (searchInput ? searchInput.value : "").trim().toLowerCase();
            var selectedStatus = statusSelect ? statusSelect.value.toLowerCase() : "all";
            var visibleCount = 0;
            var rows = page.querySelectorAll("[data-asv-row]");

            rows.forEach(function (row) {
                var rowStatus = (row.dataset.status || "").toLowerCase();
                var text = row.textContent.toLowerCase();
                var matchStatus = selectedStatus === "all" || rowStatus === selectedStatus;
                var matchSearch = !searchTerm || text.indexOf(searchTerm) > -1;
                if (matchStatus && matchSearch) {
                    row.style.display = "";
                    visibleCount++;
                } else {
                    row.style.display = "none";
                }
            });

            if (paginationInfo) paginationInfo.textContent = "Showing 1–" + visibleCount + " of " + visibleCount + " submissions";
            if (selectedCount) selectedCount.textContent = "Showing " + visibleCount + " Records";
        }

        if (searchInput) searchInput.addEventListener("input", applyFilters);
        if (statusSelect) statusSelect.addEventListener("change", applyFilters);
        if (btnResetFilters) {
            btnResetFilters.addEventListener("click", function (e) {
                e.preventDefault();
                if (searchInput) searchInput.value = "";
                if (statusSelect) statusSelect.value = "all";
                applyFilters();
            });
        }

        // -------------------------------------------------------
        // MODAL
        // -------------------------------------------------------
        function populateDossier(row) {
            if (!row) return;
            var ds = row.dataset;
            var currentStatus = (ds.status || "pending").toLowerCase();

            if (dKyc) dKyc.textContent = "#" + (ds.kyc || "");
            if (dName) dName.textContent = ds.name || "";
            if (dCourse) dCourse.textContent = ds.course || "";
            if (dChip) dChip.textContent = "#" + (ds.id || "");
            if (dCampus) dCampus.textContent = ds.campus || "";
            if (dEmail) dEmail.textContent = ds.email || "";
            if (dPhone) dPhone.textContent = ds.phone || "";
            if (dMeta) dMeta.textContent = ds.meta || "";
            if (dAvatar) dAvatar.textContent = ds.initials || "--";
            if (dCardName) dCardName.textContent = (ds.name || "").toUpperCase();
            if (dCardCourse) dCardCourse.textContent = (ds.course || "").split("•")[0];
            if (dCardId) dCardId.textContent = ds.id || "";
            if (dCorName) dCorName.textContent = ds.name || "";
            if (dCorId) dCorId.textContent = ds.id || "";

            if (dStatusPill && statusConfig[currentStatus]) {
                dStatusPill.className = statusConfig[currentStatus].className;
                dStatusPill.textContent = statusConfig[currentStatus].label;
            }

            updateActionButtons(currentStatus);
        }

        function updateActionButtons(status) {
            var isPending = status === "pending";
            if (btnApprove) btnApprove.style.display = isPending ? "" : "none";
            if (btnReject) btnReject.style.display = isPending ? "" : "none";
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
            if (docPreviewCard) {
                docPreviewCard.style.transform = "";
                previewRotated = false;
                previewZoomed = false;
            }
        }

        if (btnCloseModal) btnCloseModal.addEventListener("click", closeModal);
        if (modalOverlay) modalOverlay.addEventListener("click", closeModal);
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" || e.keyCode === 27) {
                if (lightboxOverlay && lightboxOverlay.style.display !== "none") {
                    closeLightbox();
                } else if (modalContainer && modalContainer.style.display !== "none") {
                    closeModal();
                }
            }
        });

        function wireRowEvents() {
            page.querySelectorAll(".asv-btn-open-dossier").forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var row = btn.closest("[data-asv-row]");
                    page.querySelectorAll("[data-asv-row]").forEach(function (r) { r.classList.remove("is-selected"); });
                    if (row) row.classList.add("is-selected");
                    openModal(row);
                });
            });

            page.querySelectorAll("[data-asv-row]").forEach(function (row) {
                row.addEventListener("click", function (e) {
                    if (e.target.closest("button")) return;
                    page.querySelectorAll("[data-asv-row]").forEach(function (r) { r.classList.remove("is-selected"); });
                    row.classList.add("is-selected");
                    openModal(row);
                });
            });
        }

        function updateRowStatusPill(row, status) {
            var cells = row.querySelectorAll("td");
            var statusCell = cells[5];
            if (statusCell && statusConfig[status]) {
                statusCell.innerHTML = '<span class="' + statusConfig[status].className + '">' + statusConfig[status].label + '</span>';
            }
        }

        // -------------------------------------------------------
        // TABS
        // -------------------------------------------------------
        tabs.forEach(function (tab) {
            tab.addEventListener("click", function () {
                var target = tab.dataset.asvTab;
                tabs.forEach(function (t) {
                    t.classList.remove("is-active");
                    t.setAttribute("aria-selected", "false");
                });
                tab.classList.add("is-active");
                tab.setAttribute("aria-selected", "true");
                tabPanels.forEach(function (p) {
                    if (p.dataset.tabPanel === target) p.classList.add("is-active");
                    else p.classList.remove("is-active");
                });
            });
        });

        // -------------------------------------------------------
        // LIGHTBOX
        // -------------------------------------------------------
        function openLightbox() {
            var activePanel = page.querySelector(".asv-tab-panel.is-active");
            if (!activePanel || !lightboxBody) return;

            var activeCard = activePanel.querySelector(".asv-doc-preview, .asv-cor-preview");
            if (!activeCard) return;

            lightboxBody.innerHTML = "";
            var cloned = activeCard.cloneNode(true);
            cloned.style.transform = "";
            lightboxBody.appendChild(cloned);

            if (lightboxOverlay) lightboxOverlay.style.display = "flex";
        }

        function closeLightbox() {
            if (lightboxOverlay) lightboxOverlay.style.display = "none";
        }

        if (btnCloseLightbox) btnCloseLightbox.addEventListener("click", closeLightbox);
        if (lightboxOverlay) {
            lightboxOverlay.addEventListener("click", function (e) {
                if (e.target === lightboxOverlay) closeLightbox();
            });
        }

        // -------------------------------------------------------
        // UTILITY BUTTONS
        // -------------------------------------------------------
        utilButtons.forEach(function (btn) {
            btn.addEventListener("click", function () {
                var util = btn.dataset.asvUtil;

                if (util === "expand") {
                    openLightbox();
                } else if (util === "rotate") {
                    if (!docPreviewCard) return;
                    previewRotated = !previewRotated;
                    previewZoomed = false;
                    docPreviewCard.style.transform = previewRotated ? "rotate(90deg) scale(0.85)" : "";
                } else if (util === "zoom") {
                    if (!docPreviewCard) return;
                    previewZoomed = !previewZoomed;
                    previewRotated = false;
                    docPreviewCard.style.transform = previewZoomed ? "scale(1.15)" : "";
                } else if (util === "download") {
                    var row = page.querySelector("[data-asv-row].is-selected");
                    var id = row ? row.dataset.id : "";
                    alert("Preparing high-resolution scan download for Student ID: " + id);
                }
            });
        });

        // -------------------------------------------------------
        // APPROVE / REJECT  → real POST
        // -------------------------------------------------------
        if (btnApprove) {
            btnApprove.addEventListener("click", function () {
                var row = page.querySelector("[data-asv-row].is-selected");
                if (!row) return;
                if ((row.dataset.status || "").toLowerCase() !== "pending") return;
                if (!confirm("Approve this account?")) return;

                var id = row.dataset.id;

                postForm(approveUrl, { id: id }, function (data) {
                    if (!data.success) { alert("Approval failed."); return; }
                    row.dataset.status = "approved";
                    updateRowStatusPill(row, "approved");
                    populateDossier(row);
                    applyFilters();
                });
            });
        }

        if (btnReject) {
            btnReject.addEventListener("click", function () {
                var row = page.querySelector("[data-asv-row].is-selected");
                if (!row) return;
                if ((row.dataset.status || "").toLowerCase() !== "pending") return;

                var reason = prompt("Reason for rejection (optional):");
                if (reason === null) return; // cancelled

                var id = row.dataset.id;

                postForm(rejectUrl, { id: id, reason: reason }, function (data) {
                    if (!data.success) { alert("Rejection failed."); return; }
                    row.dataset.status = "rejected";
                    updateRowStatusPill(row, "rejected");
                    populateDossier(row);
                    applyFilters();
                });
            });
        }

        // -------------------------------------------------------
        // INITIAL FETCH
        // -------------------------------------------------------
        fetch(endpoint, { headers: { "Accept": "application/json" } })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!data || !data.success) return;
                renderTable(data.data || []);
                updateKpis(data.data || []);
                applyFilters();
            })
            .catch(function (err) {
                console.error("Error loading students:", err);
                if (selectedCount) selectedCount.textContent = "Error loading records";
            });
    });
})();