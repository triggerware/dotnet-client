<script>
    import NavItem from "./NavItem.svelte";
</script>

<div>
    <NavItem>Home</NavItem>
    <NavItem>About</NavItem>
</div>

<style>
    div {
        display: flex;
        justify-content: space-around;
        padding: 1em;
        background-color: #333;
    }
</style>
