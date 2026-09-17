(() => {
    const page = document.querySelector('.campaign-details-page');
    if (!page) return;

    const tabs = Array.from(page.querySelectorAll('.campaign-tabs [data-tab]'));
    const panels = Array.from(page.querySelectorAll('.campaign-tab-panel'));
    const activate = (selected, focus = false) => {
        tabs.forEach(tab => {
            const active = tab === selected;
            tab.classList.toggle('is-active', active);
            tab.setAttribute('aria-selected', String(active));
            tab.tabIndex = active ? 0 : -1;
        });
        panels.forEach(panel => { panel.hidden = panel.dataset.panel !== selected.dataset.tab; });
        if (focus) selected.focus();
    };
    tabs.forEach((tab, index) => {
        tab.id = `campaign-tab-${tab.dataset.tab}`;
        tab.setAttribute('aria-controls', `campaign-panel-${tab.dataset.tab}`);
        tab.addEventListener('click', () => activate(tab));
        tab.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
            if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = tabs.length - 1;
            if (next !== undefined) { event.preventDefault(); activate(tabs[next], true); }
        });
    });
    panels.forEach(panel => {
        panel.id = `campaign-panel-${panel.dataset.panel}`;
        panel.setAttribute('role', 'tabpanel');
        panel.setAttribute('aria-labelledby', `campaign-tab-${panel.dataset.panel}`);
        panel.tabIndex = 0;
    });
    if (tabs.length) activate(tabs[0]);

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
