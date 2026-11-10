/* ==========================================================================
   UniShare - Dispute Management Script (Full-Width Table & Centered Modal)
   ========================================================================== */

$(document).ready(function () {
    var isAjaxLoading = false;

    // Helper: Close Modal Function
    function closeModal() {
        $("#modalOverlay").fadeOut(200);
        $("#modalContent").fadeOut(200, function () {
            $(this).empty();
        });
        $(".clickable-row").removeClass("selected");
        isAjaxLoading = false;
    }

    // 1. Row Click Event: Opens Centered Modal Form with Blur Overlay
    $(document).on("click", ".clickable-row", function (e) {
        // Prevent concurrent or duplicate AJAX requests
        if (isAjaxLoading) {
            return;
        }

        var $row = $(this);
        var disputeId = $row.data("id");

        $(".clickable-row").removeClass("selected");
        $row.addClass("selected");

        isAjaxLoading = true;

        // Show loading state inside the centered modal container
        $("#modalContent").html(
            '<div class="modal-loading">' +
            '    <div class="loading-spinner"></div>' +
            '    <p class="loading-text">Loading dispute dossier...</p>' +
            '</div>'
        );

        // Display modal overlay with blur and content container
        $("#modalOverlay").fadeIn(200);
        $("#modalContent").fadeIn(200);

        // Fetch dossier details via AJAX
        $.ajax({
            url: '/DisputeManagement/GetDisputeDetails',
            type: 'GET',
            data: { id: disputeId },
            success: function (result) {
                $("#modalContent").html(result);
                isAjaxLoading = false;
            },
            error: function () {
                $("#modalContent").html(
                    '<div class="modal-error">' +
                    '    <p class="error-text">Failed to load incident details. Please try again.</p>' +
                    '    <button type="button" class="btn btn-default" id="btnCloseModal">Close</button>' +
                    '</div>'
                );
                isAjaxLoading = false;
            }
        });
    });

    // 2. Close Modal Events (Close Button, Overlay Click, and ESC Key)
    $(document).on("click", "#btnCloseModal", function (e) {
        e.preventDefault();
        closeModal();
    });

    $(document).on("click", "#modalOverlay", function (e) {
        e.preventDefault();
        closeModal();
    });

    $(document).on("keydown", function (e) {
        if (e.key === "Escape" || e.keyCode === 27) {
            if ($("#modalOverlay").is(":visible")) {
                closeModal();
            }
        }
    });

    // 3. Filtering: Real-Time Search Bar & Status Dropdown Filter
    function filterTable() {
        var keyword = ($("#searchBox").val() || "").toString().toLowerCase().trim();
        var selectedStatus = ($("#statusFilter").val() || "").toString().toLowerCase().trim();
        var matchCount = 0;

        $(".clickable-row").each(function () {
            var $row = $(this);
            var rowKeywords = ($row.data("keywords") || "").toString().toLowerCase();
            var rowStatus = ($row.data("status") || "").toString().toLowerCase();

            var matchesKeyword = !keyword || rowKeywords.indexOf(keyword) !== -1;
            var matchesStatus = !selectedStatus || rowStatus === selectedStatus;

            if (matchesKeyword && matchesStatus) {
                $row.show();
                matchCount++;
            } else {
                $row.hide();
            }
        });

        var $recordBadge = $("#recordCount");
        if ($recordBadge.length) {
            $recordBadge.text(matchCount + (matchCount === 1 ? " Record" : " Records"));
        }
    }

    $("#searchBox").on("keyup input", filterTable);
    $("#statusFilter").on("change", filterTable);

    $("#btnReset").on("click", function (e) {
        e.preventDefault();
        $("#searchBox").val("");
        $("#statusFilter").val("");
        filterTable();
    });
});
