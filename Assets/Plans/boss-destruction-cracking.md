# implementation Plan - Boss Destruction: "Cracking" walls instead of collapsing

This plan outlines the implementation of a "cracking" effect for specific architectural objects (Walls, Towers, Castles) within the `Medieval_Castle_Set`. Instead of collapsing into a pile of rubble, these objects will fracture into pieces that stay in place, creating a visual "cracked" effect.

## Proposed Changes

### 1. BossDestruction.cs
- Remove the exclusion of "Tower" objects from the destruction logic.
- Implement a detection method to identify if an object belongs to the `Medieval_Castle_Set` and matches the keywords "Wall", "Tower", or "Castle".
- Modify the fracture logic:
    - For "Static Crack" objects: Set the resulting fragments' `Rigidbody.isKinematic` to `true`. This prevents them from falling or being pushed by physics, keeping the wall structure intact while showing the fracture lines.
    - For other objects: Maintain the current collapsing behavior with explosion forces.

## Detailed Implementation Steps

### Step 1: Update Detection Logic
Modify `OnTriggerEnter` to allow "Tower" objects to be processed.

### Step 2: Implement Identification Helper
Add a method `IsStaticCrackObject(GameObject obj)` that checks:
- If the name contains "Wall", "Tower", or "Castle" (case-insensitive).
- If any parent in the hierarchy contains "Medieval_Castle_Set".

### Step 3: Modify Fragment Physics
In `FractureObject`:
- Determine if the target is a static crack object.
- When iterating through generated fragments:
    - If it's a static crack: Set `isKinematic = true`. Do not apply explosion force.
    - Otherwise: Ensure `isKinematic = false`, set `ContinuousDynamic` collision, and apply the explosion force.

## Verification & Testing
1. **Tower Test**: Ensure the boss can now "break" Towers (which were previously ignored). They should crack but not fall.
2. **Wall Test**: Ensure objects with "Wall" in their name inside the castle set stay in place after fracturing.
3. **Prop Test**: Ensure regular props (barrels, boxes, etc.) still collapse and fly away as before.
4. **Visual Check**: Verify that the cracks are visible on the static objects.
