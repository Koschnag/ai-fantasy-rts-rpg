const tabs = [...document.querySelectorAll('[role="tab"]')];
const panels = [...document.querySelectorAll('[role="tabpanel"]')];

function activate(tab) {
  tabs.forEach((item) => {
    const selected = item === tab;
    item.setAttribute("aria-selected", String(selected));
    item.tabIndex = selected ? 0 : -1;
  });
  panels.forEach((panel) => {
    const selected = panel.id === `panel-${tab.dataset.panel}`;
    panel.hidden = !selected;
    panel.classList.toggle("active", selected);
  });
}

tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => activate(tab));
  tab.addEventListener("keydown", (event) => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    let next = index;
    if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length;
    if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = tabs.length - 1;
    activate(tabs[next]);
    tabs[next].focus();
  });
});

const bind = (name, value) => document.querySelectorAll(`[data-bind="${name}"]`).forEach((node) => { node.textContent = value; });

fetch('status.json', { headers: { Accept: 'application/json' } })
  .then((response) => response.ok ? response.json() : Promise.reject(new Error(`status ${response.status}`)))
  .then((status) => {
    bind('accepted', status.workItems.accepted);
    bind('ready', status.workItems.ready);
    bind('runtime', status.activeTask.status.toUpperCase());
    bind('runtimeTask', `${status.activeTask.id} · walking skeleton`);
    bind('shortCommit', status.commit.slice(0, 8));
    bind('generatedAt', `Stand ${new Intl.DateTimeFormat('de-DE', { dateStyle: 'medium' }).format(new Date(status.generatedAt))}`);
  })
  .catch(() => {});
