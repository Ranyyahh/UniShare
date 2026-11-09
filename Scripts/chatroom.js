
/* ==========================================
   CONVERSATION SEARCH FUNCTIONALITY
========================================== */

document.addEventListener("DOMContentLoaded", function () {

    const searchInput = document.getElementById("cr-search");
    const clearButton = document.getElementById("cr-clear-search");
    const conversationList = document.getElementById("cr-list");
    const conversationCount = document.getElementById("cr-count");
    const noResults = document.getElementById("cr-no-results");
    const searchContainer = document.querySelector(".cr-inbox-search");

    // Stop if the search elements are not available
    if (!searchInput || !conversationList) return;

    const conversations = Array.from(
        conversationList.querySelectorAll(".cr-conversation")
    );

    function filterConversations() {

        const searchValue = searchInput.value
            .toLowerCase()
            .trim();

        let visibleCount = 0;

        conversations.forEach(function (conversation) {

            const name = conversation.querySelector(
                ".cr-convo-copy b"
            )?.textContent || "";

            const message = conversation.querySelector(
                ".cr-convo-copy small:not(.cr-item-preview)"
            )?.textContent || "";

            const item = conversation.querySelector(
                ".cr-item-preview"
            )?.textContent || "";

            // Search across name, message and item
            const searchableText = (
                name + " " + message + " " + item
            ).toLowerCase();

            const matches = searchableText.includes(searchValue);

            conversation.classList.toggle(
                "search-hidden",
                !matches
            );

            if (matches) {
                visibleCount++;
            }
        });

        // Update conversation count
        if (conversationCount) {
            conversationCount.textContent = visibleCount;
        }

        // Show empty state when no matches exist
        if (noResults) {
            noResults.hidden = visibleCount !== 0;
        }

        // Toggle clear button
        if (searchContainer) {
            searchContainer.classList.toggle(
                "has-value",
                searchValue.length > 0
            );
        }
    }

    // Search as the user types
    searchInput.addEventListener("input", filterConversations);

    // Clear search
    if (clearButton) {
        clearButton.addEventListener("click", function () {

            searchInput.value = "";

            filterConversations();

            searchInput.focus();
        });
    }

});
