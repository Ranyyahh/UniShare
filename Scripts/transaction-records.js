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
    $(document).on("click", "#btnTxnCloseModal, #btnTxnFooterClose, .btn-close-modal", function (e) {
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
                } else if (selectedDateRange === "last 30 days") {
                    matchesDate = (rowDateRange === "today" || rowDateRange === "last 7 days" || rowDateRange === "last 30 days");
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

    // 5. Real payment refund action; record changes are persisted server-side.
    $(document).on("click", "#btnRefundPayment", function (e) {
        e.preventDefault();
        var $button = $(this);
        var reason = window.prompt("Reason for refund:");
        if (!reason || !reason.trim()) return;

        var $details = $("#payment-admin-details");
        var token = $details.find("input[name='__RequestVerificationToken']").val();
        $button.prop("disabled", true);
        $.ajax({
            url: $button.data("url"),
            type: "POST",
            data: { PaymentId: $button.data("payment-id"), Reason: reason.trim(), __RequestVerificationToken: token },
            success: function () {
                var $row = $(".clickable-row.selected");
                $row.attr("data-status", "REFUNDED").data("status", "REFUNDED");
                $row.children("td").eq(9).text("REFUNDED");
                $("#modalStatusBadge").removeClass("badge-confirmed badge-pending").addClass("badge-pending").text("REFUNDED");
                $("#modalAlertBox").removeClass("modal-alert-info modal-alert-danger").addClass("modal-alert-box modal-alert-success")
                    .text("Refund recorded and payment status updated.").slideDown(150);
                $button.remove();
                filterRecords();
            },
            error: function (xhr) {
                var result = xhr.responseJSON || {};
                $("#modalAlertBox").removeClass("modal-alert-success modal-alert-info").addClass("modal-alert-box modal-alert-danger")
                    .text(result.message || "Refund could not be recorded. Refresh and try again.").slideDown(150);
                $button.prop("disabled", false);
            }
        });
    });
});
