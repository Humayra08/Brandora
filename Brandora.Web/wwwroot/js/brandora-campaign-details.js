(() => {
    const page = document.querySelector('.campaign-details-page');
    if (!page) return;

    page.querySelectorAll('.campaign-tabs [data-tab]').forEach(tabButton => {
        tabButton.addEventListener('click', () => {
            page.querySelectorAll('.campaign-tabs [data-tab]').forEach(button => {
                button.classList.toggle('is-active', button === tabButton);
                button.setAttribute('aria-selected', button === tabButton ? 'true' : 'false');
            });
            page.querySelectorAll('.campaign-tab-panel').forEach(panel => {
                panel.hidden = panel.dataset.panel !== tabButton.dataset.tab;
            });
        });
    });

    page.querySelectorAll('[data-dialog]').forEach(button => {
        button.addEventListener('click', () => document.getElementById(button.dataset.dialog)?.showModal());
    });
    page.querySelectorAll('dialog').forEach(dialog => {
        dialog.addEventListener('click', event => {
            const bounds = dialog.getBoundingClientRect();
            if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) {
                dialog.close();
            }
        });
    });
})();
