function MyHeader({ html, state }) {
  return html`<style>h1 { color: red; }</style><h1><slot></slot></h1><p>Message: ${state?.store?.message || "no message"}</p>`
}

