// Node loads this file as is (vite.ts imports it with the extension), so it has no enums or JSX.
export const glowStorageKey = 'wattwise-glow'
export const glowAttribute = 'data-glow'
export const moreContrastQuery = '(prefers-contrast: more)'
export const forcedColorsQuery = '(forced-colors: active)'

// Runs in the head script before first paint. A stored `on` or `off` wins; anything else counts as
// nothing stored, and then the device decides: off for more contrast or forced colours.
export const glowScriptBody = (): string =>
  `(function(){try{var s=null;try{s=localStorage.getItem(${JSON.stringify(glowStorageKey)})}catch(e){}var off=s==='off'||(s!=='on'&&(matchMedia(${JSON.stringify(moreContrastQuery)}).matches||matchMedia(${JSON.stringify(forcedColorsQuery)}).matches));if(off){document.documentElement.setAttribute(${JSON.stringify(glowAttribute)},'off')}}catch(e){}})();`

// The head script already resolved the rule, so the attribute is the one source of truth.
export const isGlowOn = (): boolean =>
  document.documentElement.getAttribute(glowAttribute) !== 'off'

export const setGlow = (on: boolean): void => {
  try {
    localStorage.setItem(glowStorageKey, on ? 'on' : 'off')
  } catch {
    // Storage can be blocked; the choice then lasts until the page closes.
  }
  if (on) {
    document.documentElement.removeAttribute(glowAttribute)
  } else {
    document.documentElement.setAttribute(glowAttribute, 'off')
  }
}
