document.documentElement.classList.add("js-enabled");

const header = document.querySelector("[data-header]");
const menuButton = document.querySelector(".menu-toggle");
const navigation = document.querySelector(".site-nav");
const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

const updateHeader = () => {
  header?.classList.toggle("is-scrolled", window.scrollY > 16);
};

updateHeader();
window.addEventListener("scroll", updateHeader, { passive: true });

if (menuButton && navigation) {
  const closeMenu = (returnFocus = false) => {
    navigation.classList.remove("is-open");
    menuButton.setAttribute("aria-expanded", "false");
    menuButton.setAttribute("aria-label", "Abrir menu");

    if (returnFocus) {
      menuButton.focus();
    }
  };

  const openMenu = () => {
    navigation.classList.add("is-open");
    menuButton.setAttribute("aria-expanded", "true");
    menuButton.setAttribute("aria-label", "Fechar menu");
  };

  menuButton.addEventListener("click", () => {
    if (navigation.classList.contains("is-open")) {
      closeMenu();
    } else {
      openMenu();
    }
  });

  navigation.addEventListener("click", (event) => {
    if (event.target.closest("a")) {
      closeMenu();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && navigation.classList.contains("is-open")) {
      closeMenu(true);
    }
  });

  window.addEventListener("resize", () => {
    if (window.innerWidth > 1080) {
      closeMenu();
    }
  });
}

const productTabs = [...document.querySelectorAll("[data-product-tab]")];
const productPanels = [...document.querySelectorAll("[data-product-panel]")];

const activateProductTab = (tab) => {
  const selectedProduct = tab.dataset.productTab;

  productTabs.forEach((candidate) => {
    const isSelected = candidate === tab;
    candidate.setAttribute("aria-selected", String(isSelected));
    candidate.tabIndex = isSelected ? 0 : -1;
  });

  productPanels.forEach((panel) => {
    const isSelected = panel.dataset.productPanel === selectedProduct;
    panel.hidden = !isSelected;
    panel.classList.toggle("is-active", isSelected);
  });
};

productTabs.forEach((tab, index) => {
  tab.addEventListener("click", () => activateProductTab(tab));

  tab.addEventListener("keydown", (event) => {
    let destination = null;

    if (event.key === "ArrowRight") {
      destination = productTabs[(index + 1) % productTabs.length];
    } else if (event.key === "ArrowLeft") {
      destination = productTabs[(index - 1 + productTabs.length) % productTabs.length];
    } else if (event.key === "Home") {
      destination = productTabs[0];
    } else if (event.key === "End") {
      destination = productTabs.at(-1);
    }

    if (destination) {
      event.preventDefault();
      activateProductTab(destination);
      destination.focus();
    }
  });
});

const initiallySelectedTab = productTabs.find((tab) => tab.getAttribute("aria-selected") === "true");
if (initiallySelectedTab) {
  activateProductTab(initiallySelectedTab);
}

const revealElements = [...document.querySelectorAll("[data-reveal]")];

if (reducedMotion.matches || !("IntersectionObserver" in window)) {
  revealElements.forEach((element) => element.classList.add("is-visible"));
} else {
  document.documentElement.classList.add("motion-ready");

  const revealObserver = new IntersectionObserver(
    (entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) {
          return;
        }

        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      });
    },
    { rootMargin: "0px 0px -8%", threshold: 0.12 },
  );

  revealElements.forEach((element) => revealObserver.observe(element));
}

const storySteps = [...document.querySelectorAll("[data-story-step]")];
const storyScenes = [...document.querySelectorAll("[data-story-scene]")];
const storyVisual = document.querySelector("[data-story-visual]");

const activateStoryStep = (step) => {
  const selectedStep = step.dataset.storyStep;

  storySteps.forEach((candidate) => {
    candidate.classList.toggle("is-active", candidate === step);
  });

  storyScenes.forEach((scene) => {
    scene.classList.toggle("is-active", scene.dataset.storyScene === selectedStep);
  });

  if (storyVisual) {
    storyVisual.dataset.active = selectedStep;
  }
};

if (storySteps.length && "IntersectionObserver" in window) {
  const visibleSteps = new Map();
  const storyObserver = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          visibleSteps.set(entry.target, entry.intersectionRatio);
        } else {
          visibleSteps.delete(entry.target);
        }
      });

      const [currentStep] = [...visibleSteps.entries()].sort((a, b) => b[1] - a[1])[0] ?? [];
      if (currentStep) {
        activateStoryStep(currentStep);
      }
    },
    {
      rootMargin: "-24% 0px -42%",
      threshold: [0.12, 0.3, 0.5, 0.7],
    },
  );

  storySteps.forEach((step) => storyObserver.observe(step));
}
