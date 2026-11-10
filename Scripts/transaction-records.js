/* ==========================================================================
   UniShare - Transaction & Payment Records Script
   ========================================================================== */

$(document).ready(function () {
    var isAjaxLoading = false;

    // Helper: Close Modal & Reset State
    function closeModal() {
        $("#txn-modal-overlay").fadeOut(150);
        $("#txn-modal-content").fadeOut(150, function () {
            $(this).empty();
        });
        $(".clickable-row").removeClass("selected");
        isAjaxLoading = false;
    }

    // 1. Table Row Click: Open Centered Modal Form
    $(document).on("click", ".clickable-row", function (e) {
        // Prevent opening if the user clicked directly on a checkbox
        if ($(e.target).is("input[type='checkbox']") || $(e.target).closest("th, td").find("input[type='checkbox']").length > 0 && $(e.target).is("input")) {
            return;
        }

        if (isAjaxLoading) {
            return;
        }

        var $row = $(this);
        var txnId = $row.data("id");

        $(".clickable-row").removeClass("selected");
        $row.addClass("selected");

        isAjaxLoading = true;

        // Show loading state in modal
        $("#txn-modal-content").html(
            '<div class="modal-loading">' +
            '    <div class="loading-spinner"></div>' +
            '    <p class="loading-text">Loading transaction details...</p>' +
            '</div>'
        );

        $("#txn-modal-overlay").fadeIn(150);
        $("#txn-modal-content").fadeIn(150);

        // Fetch details via AJAX
        $.ajax({
            url: '/TransactionPaymentRecords/GetTransactionDetails',
            type: 'GET',
            data: { id: txnId },
            success: function (result) {
                $("#txn-modal-content").html(result);
                isAjaxLoading = false;
            },
            error: function () {
                $("#txn-modal-content").html(
                    '<div class="modal-error">' +
                    '    <p class="error-text">Failed to load transaction details.</p>' +
                    '    <button type="button" class="btn btn-default btn-xs" id="btnTxnCloseModal">Close</button>' +
                    '</div>'
                );
                isAjaxLoading = false;
            }
        });
    });

    // 2. Close Modal Handlers (Close button, Overlay click, ESC key)
    $(document).on("click", "#btnTxnCloseModal, .btn-close-modal", function (e) {
        e.preventDefault();
        closeModal();
    });

    $(document).on("click", "#txn-modal-overlay", function (e) {
        e.preventDefault();
        closeModal();
    });

    $(document).on("keydown", function (e) {
        if (e.key === "Escape" || e.keyCode === 27) {
            if ($("#txn-modal-overlay").is(":visible")) {
                closeModal();
            }
        }
    });

    // Stop propagation on row checkbox clicks
    $(document).on("click", ".txn-row-check", function (e) {
        e.stopPropagation();
    });

    $(document).on("change", "#selectAllCheckbox", function () {
        var isChecked = $(this).is(":checked");
        $(".txn-row-check:visible").prop("checked", isChecked);
    });

    // 3. Client-Side Filtering (Search Input & Dropdowns)
    function filterRecords() {
        var searchTerm = ($("#txnSearchInput").val() || "").toLowerCase().trim();
        var selectedMethod = ($("#txnMethodSelect").val() || "").toLowerCase().trim();
        var selectedStatus = ($("#txnStatusSelect").val() || "").toLowerCase().trim();
        var selectedDateRange = ($("#txnDateRangeSelect").val() || "").toLowerCase().trim();

        $("#txnRecordsTable tbody tr.clickable-row").each(function () {
            var $row = $(this);
            var searchData = ($row.data("search") || "").toLowerCase();
            var rowText = $row.text().toLowerCase();
            var rowMethod = ($row.data("method") || "").toLowerCase();
            var rowStatus = ($row.data("status") || "").toLowerCase();
            var rowDateRange = ($row.data("daterange") || "").toLowerCase();

            var matchesSearch = !searchTerm || searchData.indexOf(searchTerm) !== -1 || rowText.indexOf(searchTerm) !== -1;
            var matchesMethod = !selectedMethod || rowMethod === selectedMethod;
            var matchesStatus = !selectedStatus || rowStatus === selectedStatus;

            var matchesDate = true;
            if (selectedDateRange) {
                if (selectedDateRange === "last 7 days") {
                    matchesDate = (rowDateRange === "today" || rowDateRange === "last 7 days");
                } else if (selectedDateRange === "today") {
                    matchesDate = (rowDateRange === "today");
                }
            }

            if (matchesSearch && matchesMethod && matchesStatus && matchesDate) {
                $row.show();
            } else {
                $row.hide();
                $row.removeClass("selected");
            }
        });
    }

    $("#txnSearchInput").on("input keyup", function () {
        filterRecords();
    });

    $("#txnMethodSelect, #txnStatusSelect, #txnDateRangeSelect").on("change", function () {
        filterRecords();
    });

    $("#btnClearTxnFilters").on("click", function (e) {
        e.preventDefault();
        $("#txnSearchInput").val("");
        $("#txnMethodSelect").val("");
        $("#txnStatusSelect").val("");
        $("#txnDateRangeSelect").val("Last 7 Days");
        filterRecords();
    });

    // 4. Copy GCash Reference Number to Clipboard
    $(document).on("click", "#btnCopyGcashRef", function (e) {
        e.preventDefault();
        var refText = $("#modalGcashRefText").text().trim();
        var $btn = $(this);

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(refText).then(function () {
                $btn.html('<i class="glyphicon glyphicon-ok"></i> Copied!');
                setTimeout(function () {
                    $btn.html('<i class="glyphicon glyphicon-copy"></i> Copy');
                }, 1800);
            });
        } else {
            var $temp = $("<input>");
            $("body").append($temp);
            $temp.val(refText).select();
            document.execCommand("copy");
            $temp.remove();
            $btn.html('<i class="glyphicon glyphicon-ok"></i> Copied!');
            setTimeout(function () {
                $btn.html('<i class="glyphicon glyphicon-copy"></i> Copy');
            }, 1800);
        }
    });

    // 5. Modal Action Button Interactions
    $(document).on("click", "#btnMarkAudited", function (e) {
        e.preventDefault();
        var $badge = $("#modalStatusBadge");
        $badge.removeClass("badge-pending").addClass("badge-confirmed").text("Confirmed");

        var $activeRow = $(".clickable-row.selected");
        if ($activeRow.length > 0) {
            $activeRow.data("status", "Confirmed");
        }

        $("#modalAlertBox")
            .removeClass("modal-alert-info modal-alert-danger")
            .addClass("modal-alert-box modal-alert-success")
            .html('<strong>Audited &amp; Verified:</strong> Settlement confirmed on university ledger.')
            .slideDown(150);

        $(this).prop("disabled", true).css("opacity", "0.7");
    });

    $(document).on("click", "#btnSendReminder", function (e) {
        e.preventDefault();
        $("#modalAlertBox")
            .removeClass("modal-alert-success modal-alert-danger")
            .addClass("modal-alert-box modal-alert-info")
            .html('<strong>Reminder Dispatched:</strong> Automated notification sent to equipment lender.')
            .slideDown(150);

        $(this).prop("disabled", true).css("opacity", "0.7");
    });

    $(document).on("click", "#btnFlagDispute", function (e) {
        e.preventDefault();
        $("#modalAlertBox")
            .removeClass("modal-alert-success modal-alert-info")
            .addClass("modal-alert-box modal-alert-danger")
            .html('<strong>Flagged for Review:</strong> Transaction referred to Student Dispute Tribunal.')
            .slideDown(150);
    });
});
