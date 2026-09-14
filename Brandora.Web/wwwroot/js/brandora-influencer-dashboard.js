(() => {
    const page = document.getElementById('dashboardMain');
    const sidebar = document.getElementById('dashboardSidebar');
    const menu = document.getElementById('menuToggleBtn');
    const backdrop = document.getElementById('sidebarBackdrop');
    const mobile = window.matchMedia('(max-width: 900px)');
    const closeMobile = () => {
        sidebar.classList.remove('is-open');
        backdrop.classList.remove('is-visible');
        menu.setAttribute('aria-expanded', 'false');
    };
    const updateExpanded = () => menu.setAttribute('aria-expanded', String(mobile.matches ? sidebar.classList.contains('is-open') : !sidebar.classList.contains('is-hidden')));
    menu.addEventListener('click', () => {
        if (mobile.matches) {
            sidebar.classList.toggle('is-open');
            backdrop.classList.toggle('is-visible');
        } else {
            sidebar.classList.toggle('is-hidden');
            page.classList.toggle('sidebar-hidden');
        }
        updateExpanded();
    });
    backdrop.addEventListener('click', closeMobile);
    mobile.addEventListener('change', () => { closeMobile(); updateExpanded(); });
    updateExpanded();

    const period = document.getElementById('dashboard-period');
    if (period) {
        period.form.querySelector('button').hidden = true;
        period.addEventListener('change', () => period.form.requestSubmit());
    }
    const notifications = page.querySelector('.dashboard-notifications');
    document.addEventListener('click', event => {
        if (!notifications.contains(event.target)) notifications.open = false;
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            if (notifications.open) {
                notifications.open = false;
                notifications.querySelector('summary').focus();
            }
            if (mobile.matches && sidebar.classList.contains('is-open')) {
                closeMobile();
                menu.focus();
            }
        }
    });
})();