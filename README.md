# Virt-A-Mate Plugin for recording morphs to the animation timeline

## Usage

- Add ADD_ME.cslist to a Person
- Open plugin UI
- Choose a morph you would like to track on the animation timeline
- A HIDDEN atom has now been created with the morph name in it.  Make sure you select "show hidden" to see it
- Move the special atom atom along the X axis (red arrow direction) between 0 and 1 in the world space will be mapped percentage to the morph value.
- The hidden atom is what is actually recorded in the animation.
- To record, arm the empty atom for recording and drag it along the X asis as desired.
- If you change the morph value by some other way, the atom will also move proportion.

## License

[Attribution-ShareAlike 4.0 International](LICENSE.md)
